using Azure;
using Azure.Data.Tables;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Infrastructure.Messaging;

namespace TflAnalytics.IntegrationTests;

public sealed class LocalStackTests
{
    [Fact]
    public async Task IngestionEventsAreArchivedAndPersisted()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_LOCAL_STACK_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var storageConnectionString = GetRequiredSetting(
            "LOCAL_STORAGE_CONNECTION_STRING");
        var cosmosConnectionString = GetRequiredSetting(
            "LOCAL_COSMOS_CONNECTION_STRING");

        var blobServiceClient = new BlobServiceClient(storageConnectionString);
        var rawContainer = blobServiceClient.GetBlobContainerClient("raw");
        var archivedEventCount = 0;

        await foreach (var _ in rawContainer.GetBlobsAsync())
        {
            archivedEventCount++;
        }

        using var cosmosClient = new CosmosClient(
            cosmosConnectionString,
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                LimitToEndpoint = true,
                RequestTimeout = TimeSpan.FromSeconds(15),
                HttpClientFactory = () => new HttpClient(
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    })
            });

        var database = cosmosClient.GetDatabase("tfl-analytics");
        var arrivalCount = await CountDocumentsAsync(
            database.GetContainer("live-events"));
        var lineStatusCount = await CountDocumentsAsync(
            database.GetContainer("line-status"));

        Assert.True(archivedEventCount > 0);
        Assert.True(arrivalCount > 0);
        Assert.True(lineStatusCount > 0);
    }

    [Fact]
    public async Task LineDisruptionRunsTheAlertWorkflow()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_PHASE4_LOCAL_STACK_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var eventHubsConnectionString = GetRequiredSetting(
            "LOCAL_EVENT_HUBS_CONNECTION_STRING");
        var storageConnectionString = GetRequiredSetting(
            "LOCAL_STORAGE_CONNECTION_STRING");
        var cosmosConnectionString = GetRequiredSetting(
            "LOCAL_COSMOS_CONNECTION_STRING");
        var sqlConnectionString = GetRequiredSetting(
            "LOCAL_SQL_CONNECTION_STRING");
        var runId = Guid.NewGuid().ToString("N");
        var lineId = $"phase4-{runId}";
        var observedAt = DateTimeOffset.UtcNow;
        var goodEventId = $"phase4-good-{runId}";
        var disruptedEventId = $"phase4-disrupted-{runId}";

        await using var producer = new EventHubProducerClient(
            eventHubsConnectionString,
            "tfl-events");
        var publisher = new EventHubsEventPublisher(producer);
        using var cosmosClient = CreateCosmosClient(cosmosConnectionString);
        var lineStatus = cosmosClient
            .GetDatabase("tfl-analytics")
            .GetContainer("line-status");

        var goodEvent = CreateLineStatus(
            goodEventId,
            lineId,
            observedAt,
            10,
            "Good Service",
            null);
        await PublishUntilAsync(
            () => publisher.PublishAsync(goodEvent),
            () => DocumentExistsAsync(lineStatus, goodEventId, lineId),
            "good-service observation");

        var disruptedEvent = CreateLineStatus(
            disruptedEventId,
            lineId,
            observedAt.AddSeconds(1),
            5,
            "Part Closure",
            "A test disruption");
        await PublishUntilAsync(
            () => publisher.PublishAsync(disruptedEvent),
            () => AlertExistsAsync(sqlConnectionString, disruptedEventId),
            "SQL alert");
        await WaitForAsync(
            () => AuditExistsAsync(storageConnectionString, disruptedEventId),
            "Table audit record");
    }

    private static async Task<int> CountDocumentsAsync(Container container)
    {
        using var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        var count = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            count += response.Resource.Sum();
        }

        return count;
    }

    private static CosmosClient CreateCosmosClient(string connectionString) =>
        new(
            connectionString,
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                LimitToEndpoint = true,
                RequestTimeout = TimeSpan.FromSeconds(15),
                HttpClientFactory = () => new HttpClient(
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    })
            });

    private static EventEnvelope<LineStatusObserved> CreateLineStatus(
        string eventId,
        string lineId,
        DateTimeOffset observedAt,
        int severity,
        string description,
        string? reason) =>
        new(
            eventId,
            EventTypes.LineStatusObserved,
            "TfL",
            observedAt,
            null,
            lineId,
            1,
            new LineStatusObserved(
                lineId,
                "Phase 4 Test Line",
                severity,
                description,
                reason));

    private static async Task<bool> DocumentExistsAsync(
        Container container,
        string eventId,
        string partitionKey)
    {
        try
        {
            await container.ReadItemAsync<object>(
                eventId,
                new PartitionKey(partitionKey));
            return true;
        }
        catch (CosmosException exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static async Task<bool> AlertExistsAsync(
        string connectionString,
        string sourceEventId)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "tfl-analytics"
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(1) FROM dbo.Alerts WHERE SourceEventId = @sourceEventId";
            command.Parameters.AddWithValue("@sourceEventId", sourceEventId);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private static async Task<bool> AuditExistsAsync(
        string connectionString,
        string sourceEventId)
    {
        try
        {
            var table = new TableServiceClient(connectionString)
                .GetTableClient("audit");
            var filter = TableClient.CreateQueryFilter(
                $"PartitionKey eq {AlertRuleTypes.LineStatusDisruption} and SourceEventId eq {sourceEventId}");
            await foreach (var _ in table.QueryAsync<TableEntity>(
                               filter,
                               maxPerPage: 1))
            {
                return true;
            }

            return false;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return false;
        }
    }

    private static async Task WaitForAsync(
        Func<Task<bool>> condition,
        string description)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private static async Task PublishUntilAsync(
        Func<Task> publish,
        Func<Task<bool>> condition,
        string description)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await publish();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (await condition())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private static string GetRequiredSetting(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Set {name} before running local stack tests.");
}
