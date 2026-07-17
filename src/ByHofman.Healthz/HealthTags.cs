namespace ByHofman.Healthz;

/// <summary>Conventional tags routing health checks to the readiness and startup endpoints.</summary>
public static class HealthTags
{
    /// <summary>Checks that decide whether the app can serve traffic (readiness).</summary>
    public const string Ready = "ready";

    /// <summary>Checks that must pass once during boot (startup).</summary>
    public const string Startup = "startup";
}
