using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Infrastructure.Processing;

namespace TflAnalytics.Infrastructure.Alerts;

public sealed class TableAuditRepository : IAuditRepository
{
    private readonly TableClient _tableClient;
    private readonly ProcessingStorageOptions _options;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public TableAuditRepository(
        TableServiceClient tableServiceClient,
        IOptions<ProcessingStorageOptions> options)
    {
        _options = options.Value;
        _tableClient = tableServiceClient.GetTableClient(_options.AuditTableName);
    }

    public async Task WriteAlertRaisedAsync(
        AlertCandidate alert,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var entity = new TableEntity(alert.RuleType, alert.AlertId)
        {
            ["Action"] = "AlertRaised",
            ["SourceEventId"] = alert.SourceEventId,
            ["DetectedAtUtc"] = alert.DetectedAtUtc,
            ["ObservedAtUtc"] = alert.ObservedAtUtc,
            ["StationId"] = alert.StationId,
            ["LineId"] = alert.LineId,
            ["VehicleId"] = alert.VehicleId
        };

        await _tableClient.UpsertEntityAsync(
            entity,
            TableUpdateMode.Replace,
            cancellationToken);
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
}
