namespace TflAnalytics.Application.Processing;

public sealed record ProcessingResult(
    string EventId,
    string EventType,
    bool Created);
