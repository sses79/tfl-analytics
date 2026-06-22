using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Infrastructure.Alerts;
using TflAnalytics.Infrastructure.Processing;

namespace TflAnalytics.IntegrationTests;

public sealed class TableAlertRepositoryTests
{
    [Fact]
    public async Task StoresAlertsIdempotentlyAndReturnsNewestFirst()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_TABLE_STORAGE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connectionString = GetRequiredSetting("LOCAL_STORAGE_CONNECTION_STRING");
        var tableName = $"alerts{Guid.NewGuid():N}";
        var serviceClient = new TableServiceClient(connectionString);
        var repository = new TableAlertRepository(
            serviceClient,
            Options.Create(
                new ProcessingStorageOptions
                {
                    AlertsTableName = tableName,
                    Initialize = true
                }));

        var observedAt = DateTimeOffset.Parse("2026-06-21T12:00:00Z");
        var older = CreateAlert("older", observedAt);
        var newer = CreateAlert("newer", observedAt.AddMinutes(5));

        try
        {
            Assert.True(await repository.CreateAsync(older));
            Assert.True(await repository.CreateAsync(newer));
            Assert.False(await repository.CreateAsync(newer));

            var latest = await repository.GetRecentAlertsAsync(1);

            Assert.Equal("newer", Assert.Single(latest).AlertId);
        }
        finally
        {
            await serviceClient.DeleteTableAsync(tableName);
        }
    }

    private static AlertCandidate CreateAlert(
        string alertId,
        DateTimeOffset observedAt) =>
        new(
            alertId,
            AlertRuleTypes.ArrivalPredictionSlippage,
            $"event-{alertId}",
            observedAt.AddSeconds(5),
            observedAt,
            "940GZZLUVIC",
            "victoria",
            "245",
            "Arrival prediction delayed",
            "Test alert",
            observedAt.AddMinutes(1).ToString("O"),
            observedAt.AddMinutes(25).ToString("O"));

    private static string GetRequiredSetting(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Set {name} before running this test.");
}
