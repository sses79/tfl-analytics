using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Infrastructure.Processing;

namespace TflAnalytics.Infrastructure.Alerts;

public sealed class TableAlertRepository : IAlertRepository
{
    private const string AlertsPartitionKey = "alerts";

    private readonly TableClient _tableClient;
    private readonly ProcessingStorageOptions _options;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public TableAlertRepository(
        TableServiceClient tableServiceClient,
        IOptions<ProcessingStorageOptions> options)
    {
        _options = options.Value;
        _tableClient = tableServiceClient.GetTableClient(_options.AlertsTableName);
    }

    public async Task EnsureInitializedAsync(
        CancellationToken cancellationToken = default)
    {
        if (_initialized || !_options.Initialize)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await _tableClient.CreateIfNotExistsAsync(cancellationToken);
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<bool> CreateAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            await _tableClient.AddEntityAsync(
                AlertTableEntity.FromAlert(alert),
                cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AlertSummary>> GetRecentAlertsAsync(
        int maxCount = 50,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        await EnsureInitializedAsync(cancellationToken);

        var results = new List<AlertSummary>(maxCount);
        await foreach (var entity in _tableClient.QueryAsync<AlertTableEntity>(
                           entity => entity.PartitionKey == AlertsPartitionKey,
                           maxPerPage: maxCount,
                           cancellationToken: cancellationToken))
        {
            results.Add(entity.ToSummary());
            if (results.Count >= maxCount)
            {
                break;
            }
        }

        return results
            .OrderByDescending(alert => alert.DetectedAtUtc)
            .ToArray();
    }

    private sealed class AlertTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = AlertsPartitionKey;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string AlertId { get; set; } = string.Empty;

        public string RuleType { get; set; } = string.Empty;

        public string SourceEventId { get; set; } = string.Empty;

        public DateTimeOffset DetectedAtUtc { get; set; }

        public DateTimeOffset ObservedAtUtc { get; set; }

        public string? StationId { get; set; }

        public string? LineId { get; set; }

        public string? VehicleId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PreviousValue { get; set; } = string.Empty;

        public string CurrentValue { get; set; } = string.Empty;

        public static AlertTableEntity FromAlert(AlertCandidate alert)
        {
            var reverseObservedTicks =
                DateTime.MaxValue.Ticks - alert.ObservedAtUtc.UtcDateTime.Ticks;

            return new AlertTableEntity
            {
                RowKey = $"{reverseObservedTicks:D19}-{alert.AlertId}",
                AlertId = alert.AlertId,
                RuleType = alert.RuleType,
                SourceEventId = alert.SourceEventId,
                DetectedAtUtc = alert.DetectedAtUtc,
                ObservedAtUtc = alert.ObservedAtUtc,
                StationId = alert.StationId,
                LineId = alert.LineId,
                VehicleId = alert.VehicleId,
                Title = alert.Title,
                Description = alert.Description,
                PreviousValue = alert.PreviousValue,
                CurrentValue = alert.CurrentValue
            };
        }

        public AlertSummary ToSummary() =>
            new(
                AlertId,
                RuleType,
                StationId,
                LineId,
                Title,
                Description,
                PreviousValue,
                CurrentValue,
                DetectedAtUtc,
                ObservedAtUtc);
    }
}
