using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Messaging;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Infrastructure.Processing;

namespace TflAnalytics.Infrastructure.Messaging;

public sealed class CosmosRawEventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly Container _container;
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosOptions _options;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public CosmosRawEventPublisher(
        CosmosClient cosmosClient,
        IOptions<CosmosOptions> options)
    {
        var value = options.Value;
        _cosmosClient = cosmosClient;
        _options = value;
        _container = cosmosClient.GetContainer(
            value.DatabaseName,
            value.RawEventsContainerName);
    }

    public async Task PublishAsync<TPayload>(
        EventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var partitionKey = envelope.StationId ?? envelope.LineId ?? envelope.EventType;
        var json = CreateDocumentJson(envelope, partitionKey);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var response = await _container.UpsertItemStreamAsync(
            stream,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new CosmosException(
                "Raw event upsert failed.",
                response.StatusCode,
                subStatusCode: 0,
                activityId: response.Headers.ActivityId,
                requestCharge: response.Headers.RequestCharge);
        }
    }

    internal static string CreateDocumentJson<TPayload>(
        EventEnvelope<TPayload> envelope,
        string partitionKey)
    {
        var document = JsonSerializer.SerializeToNode(envelope, SerializerOptions)
            as JsonObject
            ?? throw new InvalidOperationException("Event envelope did not serialize to an object.");

        document["id"] = envelope.EventId;
        document["partitionKey"] = partitionKey;

        return document.ToJsonString(SerializerOptions);
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
                    _options.RawEventsContainerName,
                    "/partitionKey")
                {
                    DefaultTimeToLive = 14400
                },
                cancellationToken: cancellationToken);
            await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(
                    _options.LeasesContainerName,
                    "/id"),
                cancellationToken: cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
