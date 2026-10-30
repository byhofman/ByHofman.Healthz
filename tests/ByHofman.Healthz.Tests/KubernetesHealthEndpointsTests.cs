using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ByHofman.Healthz.Tests;

public class KubernetesHealthEndpointsTests
{
    private static async Task<HttpClient> StartAsync(
        Action<IHealthChecksBuilder>? checks = null,
        Action<KubernetesHealthOptions>? configure = null,
        TimeProvider? time = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        if (time is not null)
            builder.Services.AddSingleton(time);

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
            checks => checks.AddCheck("untagged", () => HealthCheckResult.Unhealthy(), tags: ["other"]));

        var response = await client.GetAsync("/readyz");

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

    [Fact]
    public async Task Repeated_requests_within_the_cache_window_run_the_checks_once()
    {
        var runs = 0;
        var client = await StartAsync(
            checks => checks.AddCheck("db", () => { Interlocked.Increment(ref runs); return HealthCheckResult.Healthy(); }, tags: [HealthTags.Ready]),
            time: new FakeTimeProvider());

        for (var i = 0; i < 10; i++)
            await client.GetAsync("/readyz");

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Checks_run_again_once_the_cache_window_has_passed()
    {
        var runs = 0;
        var time = new FakeTimeProvider();
        var client = await StartAsync(
            checks => checks.AddCheck("db", () => { Interlocked.Increment(ref runs); return HealthCheckResult.Healthy(); }, tags: [HealthTags.Ready]),
            options => options.CacheDuration = TimeSpan.FromSeconds(5),
            time);

        await client.GetAsync("/readyz");
        time.Advance(TimeSpan.FromSeconds(5));
        await client.GetAsync("/readyz");

        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task Concurrent_requests_share_one_run_of_the_checks()
    {
        var runs = 0;
        var release = new TaskCompletionSource();
        var client = await StartAsync(
            checks => checks.AddAsyncCheck("db", async () =>
            {
                Interlocked.Increment(ref runs);
                await release.Task;
                return HealthCheckResult.Healthy();
            }, tags: [HealthTags.Ready]),
            time: new FakeTimeProvider());

        var requests = Enumerable.Range(0, 10).Select(_ => client.GetAsync("/readyz")).ToArray();
        release.SetResult();
        var responses = await Task.WhenAll(requests);

        Assert.Equal(1, runs);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task A_cached_unhealthy_report_keeps_its_status_code()
    {
        var client = await StartAsync(
            checks => checks.AddCheck("db", () => HealthCheckResult.Unhealthy(), tags: [HealthTags.Ready]),
            time: new FakeTimeProvider());

        await client.GetAsync("/readyz");
        var response = await client.GetAsync("/readyz");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_zero_cache_duration_runs_the_checks_on_every_request()
    {
        var runs = 0;
        var client = await StartAsync(
            checks => checks.AddCheck("db", () => { Interlocked.Increment(ref runs); return HealthCheckResult.Healthy(); }, tags: [HealthTags.Ready]),
            options => options.CacheDuration = TimeSpan.Zero);

        await client.GetAsync("/readyz");
        await client.GetAsync("/readyz");

        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task The_response_tells_clients_not_to_cache_it()
    {
        var client = await StartAsync();

        var response = await client.GetAsync("/livez");

        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
