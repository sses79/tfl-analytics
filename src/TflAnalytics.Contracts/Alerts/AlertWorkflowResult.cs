namespace TflAnalytics.Contracts.Alerts;

public sealed record AlertWorkflowResult(
    string AlertId,
    bool Created,
    bool Audited,
    bool Notified);
