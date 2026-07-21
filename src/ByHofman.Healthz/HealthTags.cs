namespace ByHofman.Healthz;

/// <summary>Conventional tags routing health checks to the readiness endpoint.</summary>
public static class HealthTags
{
    /// <summary>Checks that decide whether the app can serve traffic (readiness).</summary>
    public const string Ready = "ready";
}
