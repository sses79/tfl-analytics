using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.UnitTests;

public sealed class ArrivalDemoPollingControlTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-02T12:03:00Z");

    [Fact]
    public async Task BaselineOnlyPollsOnFiveMinuteBoundaries()
    {
        var control = new ArrivalDemoPollingControl(new RecordingStore());

        var skipped = await control.EvaluateAsync(Now);
        var due = await control.EvaluateAsync(Now.AddMinutes(2));

        Assert.False(skipped.ShouldPoll);
        Assert.Equal("baseline-skip", skipped.Reason);
        Assert.True(due.ShouldPoll);
        Assert.False(due.DemoBoostActive);
        Assert.Equal("five-minute-boundary", due.Reason);
    }

    [Fact]
    public async Task ActiveBoostPollsEveryMinuteAndExpiresAutomatically()
    {
        var store = new RecordingStore();
        var control = new ArrivalDemoPollingControl(store);
        var expiry = await control.EnableAsync(Now, TimeSpan.FromMinutes(10));

        var active = await control.EvaluateAsync(Now.AddMinutes(1));
        var expired = await control.EvaluateAsync(expiry.AddSeconds(1));

        Assert.True(active.ShouldPoll);
        Assert.True(active.DemoBoostActive);
        Assert.Equal(expiry, active.DemoBoostExpiresAtUtc);
        Assert.False(expired.DemoBoostActive);
        Assert.False(expired.ShouldPoll);
    }

    [Fact]
    public async Task StoreFailureFallsBackToFiveMinuteBaseline()
    {
        var control = new ArrivalDemoPollingControl(new RecordingStore { ThrowOnRead = true });

        var skipped = await control.EvaluateAsync(Now);
        var due = await control.EvaluateAsync(Now.AddMinutes(2));

        Assert.False(skipped.ShouldPoll);
        Assert.Equal("control-failure-skip", skipped.Reason);
        Assert.True(due.ShouldPoll);
        Assert.Equal("control-failure-boundary", due.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task RejectsInvalidBoostDuration(int minutes)
    {
        var control = new ArrivalDemoPollingControl(new RecordingStore());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => control.EnableAsync(Now, TimeSpan.FromMinutes(minutes)));
    }

    private sealed class RecordingStore : IArrivalDemoPollingStore
    {
        public DateTimeOffset? Expiry { get; private set; }
        public bool ThrowOnRead { get; init; }

        public Task<DateTimeOffset?> GetExpiryAsync(
            CancellationToken cancellationToken = default) =>
            ThrowOnRead
                ? throw new InvalidOperationException("Unavailable")
                : Task.FromResult(Expiry);

        public Task SetExpiryAsync(
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            Expiry = expiresAtUtc;
            return Task.CompletedTask;
        }
    }
}
