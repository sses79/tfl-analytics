using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Infrastructure.Processing;

public sealed class CosmosEventRepository : IEventRepository
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
}
