namespace TflAnalytics.Contracts.Processing;

public sealed record ProcessingMessage(
    string EventId,
    string EventType,
    string ArchivePath,
    DateTimeOffset EnqueuedAtUtc);
