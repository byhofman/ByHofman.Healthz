using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ByHofman.Healthz;

/// <summary>One endpoint's checks, run at most once per cache window however many requests arrive.</summary>
internal sealed class HealthProbe(
    Func<HealthCheckRegistration, bool> predicate,
    TimeSpan cacheDuration,
    TimeProvider timeProvider)
{
    private readonly Lock _gate = new();
    private Task<HealthReport>? _report;
    private long _startedAt;

    public Task<HealthReport> GetReportAsync(HealthCheckService healthChecks)
    {
        if (cacheDuration <= TimeSpan.Zero)
            return healthChecks.CheckHealthAsync(predicate);

        lock (_gate)
        {
            if (_report is null || IsStale(_report))
            {
                // Not the request's token: the run is shared, and one caller hanging up must not cancel it for the rest.
                _report = healthChecks.CheckHealthAsync(predicate);
                _startedAt = timeProvider.GetTimestamp();
            }

            return _report;
        }
    }

    private bool IsStale(Task<HealthReport> report) =>
        report.IsCompleted
        && (!report.IsCompletedSuccessfully || timeProvider.GetElapsedTime(_startedAt) >= cacheDuration);
}
