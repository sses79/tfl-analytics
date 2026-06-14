using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Processing.Functions.Functions;

public static class AlertOrchestration
{
    private static readonly TaskOptions ActivityOptions = TaskOptions.FromRetryPolicy(
        new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2,
            maxRetryInterval: TimeSpan.FromSeconds(30),
            retryTimeout: TimeSpan.FromMinutes(2)));

    [Function(nameof(AlertOrchestration))]
    public static async Task<AlertWorkflowResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var alert = context.GetInput<AlertCandidate>()
            ?? throw new InvalidDataException("Alert workflow input is required.");

        var created = await context.CallActivityAsync<bool>(
            nameof(AlertActivities.PersistAlert),
            alert,
            ActivityOptions);
        await context.CallActivityAsync(
            nameof(AlertActivities.WriteAlertAudit),
            alert,
            ActivityOptions);
        await context.CallActivityAsync(
            nameof(AlertActivities.SendMockAlertNotification),
            alert,
            ActivityOptions);

        return new AlertWorkflowResult(alert.AlertId, created, true, true);
    }
}
