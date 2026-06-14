using System.Globalization;
using System.IO.Compression;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Infrastructure.Processing;

public sealed class BlobRawEventArchive : IRawEventArchive
{
    private readonly BlobContainerClient _containerClient;
    private readonly bool _initialize;

    public BlobRawEventArchive(
        BlobContainerClient containerClient,
        IOptions<ProcessingStorageOptions> options)
    {
        _containerClient = containerClient;
        _initialize = options.Value.Initialize;
    }

    public async Task<string> WriteAsync(
        RawEvent rawEvent,
        CancellationToken cancellationToken = default)
    {
        if (_initialize)
        {
            await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);
        }

        var archivePath = BuildArchivePath(rawEvent);
        var blobClient = _containerClient.GetBlobClient(archivePath);

        await using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        await using (var writer = new StreamWriter(
            gzip,
            new UTF8Encoding(false),
            leaveOpen: true))
        {
            await writer.WriteAsync(rawEvent.Json.AsMemory(), cancellationToken);
        }

        compressed.Position = 0;
        await blobClient.UploadAsync(
            compressed,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json",
                    ContentEncoding = "gzip"
                }
            },
            cancellationToken);

        return archivePath;
    }

    public async Task<string> ReadAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        var response = await _containerClient
            .GetBlobClient(archivePath)
            .DownloadStreamingAsync(cancellationToken: cancellationToken);

        await using var gzip = new GZipStream(
            response.Value.Content,
            CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string BuildArchivePath(RawEvent rawEvent)
    {
        var observed = rawEvent.ObservedAtUtc.UtcDateTime;
        var eventType = rawEvent.EventType switch
        {
            EventTypes.ArrivalPredictionObserved => "arrival",
            EventTypes.LineStatusObserved => "line-status",
            _ => throw new InvalidDataException(
                $"Unsupported event type '{rawEvent.EventType}'.")
        };
        var partition = rawEvent.StationId is not null
            ? $"stationId={Uri.EscapeDataString(rawEvent.StationId)}"
            : $"lineId={Uri.EscapeDataString(rawEvent.LineId!)}";

        return string.Join(
            '/',
            $"eventType={eventType}",
            $"year={observed.Year.ToString("0000", CultureInfo.InvariantCulture)}",
            $"month={observed.Month.ToString("00", CultureInfo.InvariantCulture)}",
            $"day={observed.Day.ToString("00", CultureInfo.InvariantCulture)}",
            $"hour={observed.Hour.ToString("00", CultureInfo.InvariantCulture)}",
            partition,
            $"{rawEvent.EventId}.json.gz");
    }
}
