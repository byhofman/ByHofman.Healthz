using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ByHofman.Healthz;

/// <summary>Maps Kubernetes-style health endpoints onto the built-in ASP.NET Core health checks.</summary>
public static class KubernetesHealthEndpoints
{
    /// <summary>
    /// Maps <c>/livez</c> and <c>/readyz</c> (and, per options, <c>/healthz</c>).
    /// Requires the health-check services to be registered (<c>AddHealthChecks()</c>).
    /// Each endpoint reuses its report for <see cref="KubernetesHealthOptions.CacheDuration"/>.
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
            : HealthResponseWriter.WritePlainTextAsync;
        var timeProvider = endpoints.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

        HealthProbe Probe(Func<HealthCheckRegistration, bool> predicate) =>
            new(predicate, options.CacheDuration, timeProvider);

        MapProbe(endpoints, options.LivenessPath, Probe(_ => false), writer);
        MapProbe(endpoints, options.ReadinessPath, Probe(registration => registration.Tags.Contains(options.ReadyTag)), writer);

        if (options.IncludeHealthz)
            MapProbe(endpoints, options.HealthzPath, Probe(_ => true), writer);

        return endpoints;
    }

    private static void MapProbe(
        IEndpointRouteBuilder endpoints,
        string path,
        HealthProbe probe,
        Func<HttpContext, HealthReport, Task> writer)
    {
        endpoints.Map(path, async context =>
        {
            var healthChecks = context.RequestServices.GetRequiredService<HealthCheckService>();
            var report = await probe.GetReportAsync(healthChecks);

            context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;
            context.Response.Headers.CacheControl = "no-store, no-cache";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "Thu, 01 Jan 1970 00:00:00 GMT";

            await writer(context, report);
        });
    }
}
