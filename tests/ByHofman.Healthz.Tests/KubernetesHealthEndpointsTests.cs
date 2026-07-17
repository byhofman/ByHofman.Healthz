using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ByHofman.Healthz.Tests;

public class KubernetesHealthEndpointsTests
{
    private static async Task<HttpClient> StartAsync(
        Action<IHealthChecksBuilder>? checks = null,
        Action<KubernetesHealthOptions>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        var healthChecks = builder.Services.AddHealthChecks();
        checks?.Invoke(healthChecks);

        var app = builder.Build();
        app.MapKubernetesHealthChecks(configure);
        await app.StartAsync();

        return app.GetTestClient();
    }

    [Fact]
    public async Task Livez_is_healthy_with_no_checks()
    {
        var client = await StartAsync();

        var response = await client.GetAsync("/livez");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readyz_is_unhealthy_when_a_ready_check_fails()
    {
        var client = await StartAsync(
            checks => checks.AddCheck("db", () => HealthCheckResult.Unhealthy(), tags: [HealthTags.Ready]));

        var response = await client.GetAsync("/readyz");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Readyz_ignores_checks_not_tagged_ready()
    {
        var client = await StartAsync(
            checks => checks.AddCheck("startup-only", () => HealthCheckResult.Unhealthy(), tags: [HealthTags.Startup]));

        var response = await client.GetAsync("/readyz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startupz_is_not_mapped_by_default()
    {
        var client = await StartAsync();

        var response = await client.GetAsync("/startupz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Startupz_is_mapped_when_enabled()
    {
        var client = await StartAsync(configure: options => options.MapStartup = true);

        var response = await client.GetAsync("/startupz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Healthz_can_be_disabled()
    {
        var client = await StartAsync(configure: options => options.IncludeHealthz = false);

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Healthz_returns_json_when_enabled()
    {
        var client = await StartAsync(
            checks => checks.AddCheck("db", () => HealthCheckResult.Healthy("ok"), tags: [HealthTags.Ready]),
            options => options.WriteJsonResponse = true);

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\"", body);
        Assert.Contains("db", body);
    }
}
