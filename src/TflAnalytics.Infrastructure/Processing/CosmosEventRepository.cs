using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Infrastructure.Processing;

public sealed class CosmosEventRepository : IEventRepository, IObservationHistory
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosOptions _options;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public CosmosEventRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosOptions> options)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
    }

    public Task<bool> CreateArrivalAsync(
        EventEnvelope<ArrivalPredictionObserved> envelope,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            _options.LiveEventsContainerName,
            new ArrivalEventDocument(
                envelope.EventId,
                envelope.EventType,
                envelope.Source,
                envelope.ObservedAtUtc,
                envelope.StationId!,
                envelope.LineId!,
                envelope.SchemaVersion,
                envelope.Payload),
            new PartitionKey(envelope.StationId),
            cancellationToken);

    public Task<bool> CreateLineStatusAsync(
        EventEnvelope<LineStatusObserved> envelope,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            _options.LineStatusContainerName,
            new LineStatusEventDocument(
                envelope.EventId,
                envelope.EventType,
                envelope.Source,
                envelope.ObservedAtUtc,
                envelope.LineId!,
                envelope.SchemaVersion,
                envelope.Payload),
            new PartitionKey(envelope.LineId),
            cancellationToken);

    public async Task<ArrivalObservation?> GetPreviousArrivalAsync(
        EventEnvelope<ArrivalPredictionObserved> current,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(current.StationId)
            || string.IsNullOrWhiteSpace(current.Payload.VehicleId))
        {
            return null;
        }

        var query = new QueryDefinition(
                """
                SELECT TOP 2
                    c.id AS id,
                    c.observedAtUtc AS observedAtUtc,
                    c.payload.ExpectedArrivalUtc AS expectedArrivalUtc,
                    c.payload.Direction AS direction
                FROM c
                WHERE c.id != @eventId
                    AND c.payload.VehicleId = @vehicleId
                    AND c.lineId = @lineId
                ORDER BY c.observedAtUtc DESC
                """)
            .WithParameter("@eventId", current.EventId)
            .WithParameter("@vehicleId", current.Payload.VehicleId)
            .WithParameter("@lineId", current.LineId);

        var container = _cosmosClient.GetContainer(
            _options.DatabaseName,
            _options.LiveEventsContainerName);
        using var iterator = container.GetItemQueryIterator<ArrivalHistoryDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(current.StationId),
                MaxItemCount = 2
            });

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        var previous = response.Resource.FirstOrDefault();
        if (previous is null)
        {
            return null;
        }

        var priorToPrevious = response.Resource.Skip(1).FirstOrDefault();
        return new ArrivalObservation(
            previous.Id,
            previous.ObservedAtUtc,
            previous.ExpectedArrivalUtc,
            priorToPrevious?.ExpectedArrivalUtc,
            previous.Direction);
    }

    public async Task<LineStatusObservation?> GetPreviousLineStatusAsync(
        EventEnvelope<LineStatusObserved> current,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(current.LineId))
        {
            return null;
        }

        var query = new QueryDefinition(
                """
                SELECT TOP 1
                    c.id AS id,
                    c.observedAtUtc AS observedAtUtc,
                    c.payload.StatusSeverity AS statusSeverity,
                    c.payload.StatusSeverityDescription AS statusSeverityDescription
                FROM c
                WHERE c.id != @eventId
                ORDER BY c.observedAtUtc DESC
                """)
            .WithParameter("@eventId", current.EventId);

        var container = _cosmosClient.GetContainer(
            _options.DatabaseName,
            _options.LineStatusContainerName);
        using var iterator = container.GetItemQueryIterator<LineStatusHistoryDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(current.LineId),
                MaxItemCount = 1
            });

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        var previous = response.Resource.FirstOrDefault();
        return previous is null
            ? null
            : new LineStatusObservation(
                previous.Id,
                previous.ObservedAtUtc,
                previous.StatusSeverity,
                previous.StatusSeverityDescription);
    }

    public async Task<IReadOnlyList<ArrivalSummary>> GetRecentArrivalsAsync(
        string stationId,
        int maxCount = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var query = new QueryDefinition(
            "SELECT c.lineId, c.observedAtUtc, c.payload FROM c ORDER BY c.observedAtUtc DESC");

        var container = _cosmosClient.GetContainer(
            _options.DatabaseName,
            _options.LiveEventsContainerName);
        using var iterator = container.GetItemQueryIterator<ArrivalQueryDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(stationId),
                MaxItemCount = maxCount
            });

        var results = new List<ArrivalSummary>();
        while (iterator.HasMoreResults && results.Count < maxCount)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                results.Add(new ArrivalSummary(
                    doc.LineId,
                    doc.Payload.LineName,
                    doc.Payload.DestinationName,
                    doc.Payload.PlatformName,
                    doc.Payload.Direction,
                    doc.Payload.ExpectedArrivalUtc,
                    doc.Payload.SecondsToStation,
                    doc.ObservedAtUtc));

                if (results.Count >= maxCount)
                {
                    break;
                }
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<LineStatusSummary>> GetCurrentLineStatusAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var query = new QueryDefinition(
            "SELECT c.lineId, c.observedAtUtc, c.payload FROM c ORDER BY c._ts DESC");

        var container = _cosmosClient.GetContainer(
            _options.DatabaseName,
            _options.LineStatusContainerName);
        using var iterator = container.GetItemQueryIterator<LineStatusQueryDocument>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 50 });

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<LineStatusSummary>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                if (!seen.Add(doc.LineId))
                {
                    continue;
                }

                results.Add(new LineStatusSummary(
                    doc.LineId,
                    doc.Payload.LineName,
                    doc.Payload.StatusSeverity,
                    doc.Payload.StatusSeverityDescription,
                    doc.Payload.Reason,
                    doc.ObservedAtUtc));
            }
        }

        return results;
    }

    private async Task<bool> CreateAsync<TDocument>(
        string containerName,
        TDocument document,
        PartitionKey partitionKey,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var container = _cosmosClient.GetContainer(_options.DatabaseName, containerName);

        try
        {
            await container.CreateItemAsync(
                document,
                partitionKey,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException exception)
            when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized || !_options.Initialize)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                cancellationToken: cancellationToken);

            await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(
                    _options.LiveEventsContainerName,
                    "/stationId")
                {
                    DefaultTimeToLive = _options.DefaultTtlSeconds
                },
                cancellationToken: cancellationToken);
            await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(
                    _options.LineStatusContainerName,
                    "/lineId")
                {
                    DefaultTimeToLive = _options.DefaultTtlSeconds
                },
                cancellationToken: cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private sealed record ArrivalEventDocument(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("eventType")] string EventType,
        [property: JsonProperty("source")] string Source,
        [property: JsonProperty("observedAtUtc")] DateTimeOffset ObservedAtUtc,
        [property: JsonProperty("stationId")] string StationId,
        [property: JsonProperty("lineId")] string LineId,
        [property: JsonProperty("schemaVersion")] int SchemaVersion,
        [property: JsonProperty("payload")] ArrivalPredictionObserved Payload);

    private sealed record LineStatusEventDocument(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("eventType")] string EventType,
        [property: JsonProperty("source")] string Source,
        [property: JsonProperty("observedAtUtc")] DateTimeOffset ObservedAtUtc,
        [property: JsonProperty("lineId")] string LineId,
        [property: JsonProperty("schemaVersion")] int SchemaVersion,
        [property: JsonProperty("payload")] LineStatusObserved Payload);

    private sealed record ArrivalHistoryDocument(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("observedAtUtc")] DateTimeOffset ObservedAtUtc,
        [property: JsonProperty("expectedArrivalUtc")] DateTimeOffset? ExpectedArrivalUtc,
        [property: JsonProperty("direction")] string? Direction);

    private sealed record LineStatusHistoryDocument(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("observedAtUtc")] DateTimeOffset ObservedAtUtc,
        [property: JsonProperty("statusSeverity")] int StatusSeverity,
        [property: JsonProperty("statusSeverityDescription")] string StatusSeverityDescription);

    private sealed record ArrivalQueryDocument(
        [property: JsonProperty("lineId")] string LineId,
        [property: JsonProperty("observedAtUtc")] DateTimeOffset ObservedAtUtc,
        [property: JsonProperty("payload")] ArrivalPredictionObserved Payload);

    private sealed record LineStatusQueryDocument(
        [property: JsonProperty("lineId")] string LineId,
        [property: JsonProperty("observedAtUtc")] DateTimeOffset ObservedAtUtc,
        [property: JsonProperty("payload")] LineStatusObserved Payload);
}
