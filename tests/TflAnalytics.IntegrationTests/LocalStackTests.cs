using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;

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

    private static string GetRequiredSetting(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Set {name} before running local stack tests.");
}
