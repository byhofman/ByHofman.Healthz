using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ByHofman.Healthz;

/// <summary>Maps Kubernetes-style health endpoints onto the built-in ASP.NET Core health checks.</summary>
public static class KubernetesHealthEndpoints
{
    /// <summary>
    /// Maps <c>/livez</c> and <c>/readyz</c> (and, per options, <c>/healthz</c>).
    /// Requires the health-check services to be registered (<c>AddHealthChecks()</c>).
    /// </summary>
    public static IEndpointRouteBuilder MapKubernetesHealthChecks(
        this IEndpointRouteBuilder endpoints,
        Action<KubernetesHealthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new KubernetesHealthOptions();
        configure?.Invoke(options);

        var writer = options.WriteJsonResponse
            ? (Func<HttpContext, HealthReport, Task>)HealthResponseWriter.WriteJsonAsync
            : null;

        MapProbe(endpoints, options.LivenessPath, _ => false, writer);
        MapProbe(endpoints, options.ReadinessPath, registration => registration.Tags.Contains(options.ReadyTag), writer);

        if (options.IncludeHealthz)
            MapProbe(endpoints, options.HealthzPath, _ => true, writer);

        return endpoints;
    }

    private static void MapProbe(
        IEndpointRouteBuilder endpoints,
        string path,
        Func<HealthCheckRegistration, bool> predicate,
        Func<HttpContext, HealthReport, Task>? writer)
    {
        var healthOptions = new HealthCheckOptions { Predicate = predicate };
        if (writer is not null)
            healthOptions.ResponseWriter = writer;

        endpoints.MapHealthChecks(path, healthOptions);
    }
}
