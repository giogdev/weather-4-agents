namespace Weather4Agents.Infrastructure.Diagnostics;

/// <summary>
/// Shared, thread-safe record of when the background scraping job last completed a cycle in
/// which at least one scrape succeeded. The health check reads it to decide whether the instance
/// is producing fresh data; the job writes it. A singleton so the writing job and the reading
/// health check see the same value.
/// </summary>
public sealed class ScrapeCycleTracker
{
    private readonly TimeProvider _timeProvider;

    // long ticks written/read atomically; 0 means "no successful cycle yet".
    private long _lastSuccessfulCycleTicks;

    public ScrapeCycleTracker(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The UTC time of the last cycle that had at least one successful scrape, or <c>null</c> if
    /// no such cycle has completed yet.
    /// </summary>
    public DateTimeOffset? LastSuccessfulCycleAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessfulCycleTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>Stamps the current time as the last successful scraping cycle.</summary>
    public void MarkCycleSucceeded()
        => Interlocked.Exchange(ref _lastSuccessfulCycleTicks, _timeProvider.GetUtcNow().UtcTicks);
}
