namespace ByHofman.Healthz;

/// <summary>Configures the Kubernetes health endpoints mapped by <c>MapKubernetesHealthChecks</c>.</summary>
public sealed class KubernetesHealthOptions
{
    /// <summary>Liveness path. Runs no checks. Default <c>/livez</c>.</summary>
    public string LivenessPath { get; set; } = "/livez";

    /// <summary>Readiness path. Runs checks tagged <see cref="ReadyTag"/>. Default <c>/readyz</c>.</summary>
    public string ReadinessPath { get; set; } = "/readyz";

    /// <summary>Aggregate path. Runs all checks. Default <c>/healthz</c>. Mapped when <see cref="IncludeHealthz"/> is true.</summary>
    public string HealthzPath { get; set; } = "/healthz";

    /// <summary>Map the aggregate <see cref="HealthzPath"/> endpoint. Default <c>true</c>.</summary>
    public bool IncludeHealthz { get; set; } = true;

    /// <summary>Tag selecting readiness checks. Default <see cref="HealthTags.Ready"/>.</summary>
    public string ReadyTag { get; set; } = HealthTags.Ready;

    /// <summary>Write a JSON body (status + per-check detail) instead of plain text. Default <c>false</c>.</summary>
    public bool WriteJsonResponse { get; set; }

    /// <summary>
    /// How long a probe's report is reused before its checks run again, so a flood of requests
    /// cannot turn into a flood of checks. Concurrent requests always share one run.
    /// <see cref="TimeSpan.Zero"/> runs the checks on every request. Default 5 seconds.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(5);
}
