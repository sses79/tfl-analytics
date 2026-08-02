using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.Infrastructure.Ingestion;

public sealed class BlobArrivalDemoPollingStore : IArrivalDemoPollingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BlobContainerClient _container;
    private readonly string _blobName;

    public BlobArrivalDemoPollingStore(IOptions<DemoPollingStorageOptions> options)
    {
        var value = options.Value;
        var service = !string.IsNullOrWhiteSpace(value.ConnectionString)
            ? new BlobServiceClient(value.ConnectionString)
            : new BlobServiceClient(
                new Uri($"https://{RequireAccountName(value.AccountName)}.blob.core.windows.net"),
                new DefaultAzureCredential());
        _container = service.GetBlobContainerClient(value.ContainerName);
        _blobName = value.BlobName;
    }

    public async Task<DateTimeOffset?> GetExpiryAsync(
        CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(_blobName);
        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToObjectFromJson<ControlRecord>(JsonOptions)?.ExpiresAtUtc;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task SetExpiryAsync(
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);
        await _container.GetBlobClient(_blobName).UploadAsync(
            BinaryData.FromObjectAsJson(new ControlRecord(expiresAtUtc), JsonOptions),
            overwrite: true,
            cancellationToken);
    }

    private static string RequireAccountName(string? accountName) =>
        !string.IsNullOrWhiteSpace(accountName)
            ? accountName
            : throw new InvalidOperationException(
                "Configure DemoPollingStorage:ConnectionString locally or AccountName in Azure.");

    private sealed record ControlRecord(DateTimeOffset ExpiresAtUtc);
}
