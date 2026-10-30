# ByHofman.Healthz

Kubernetes-style health endpoints for ASP.NET Core, built on the framework's own health checks.
No new health-check engine — just the `-z` endpoints wired to the standard `IHealthCheck` +
tags model.

## Endpoints

| Path        | Selects              | Probe        | Mapped by default |
|-------------|----------------------|--------------|-------------------|
| `/livez`    | no checks            | liveness     | yes               |
| `/readyz`   | checks tagged `ready`| readiness    | yes               |
| `/healthz`  | all checks           | (aggregate)  | yes (toggle off)  |

`/livez`, `/readyz` and `/healthz` are the endpoint names the Kubernetes API server itself
exposes. The startup *probe* is a probe type, not a named endpoint — the common pattern is to
point your `startupProbe` at `/livez` with a generous `failureThreshold`.

## Install

```bash
dotnet add package ByHofman.Healthz
```

## Use

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseCheck>("db", tags: [HealthTags.Ready]);

var app = builder.Build();

app.MapKubernetesHealthChecks();
// or configure:
app.MapKubernetesHealthChecks(o =>
{
    o.IncludeHealthz = false;     // drop the /healthz aggregate (default true)
    o.WriteJsonResponse = true;   // JSON body instead of plain text (default false)
    // every path and tag is configurable too
});
```

Tags are exposed as constants so you never hard-code strings:

```csharp
HealthTags.Ready    // "ready"
```

## Kubernetes probes

```yaml
startupProbe:   { httpGet: { path: /livez },  failureThreshold: 30, periodSeconds: 10 }
livenessProbe:  { httpGet: { path: /livez },  periodSeconds: 10 }
readinessProbe: { httpGet: { path: /readyz }, periodSeconds: 10 }
```

Liveness runs no checks (a dependency blip must never restart the pod); readiness runs the
`ready`-tagged checks (pulled from the load balancer, not restarted, when they fail).

Status codes follow the framework defaults: `Healthy`/`Degraded` → `200`, `Unhealthy` → `503`.

## Caching

Health endpoints are public and cheap to call, and the checks behind them (a database round trip, a
downstream ping) are not. Each endpoint therefore reuses its report for `CacheDuration` (default 5
seconds): however many requests arrive, the checks run at most once per window, and concurrent requests
share the run in flight. The response itself is still sent `no-store`, so no proxy serves a stale status.

```csharp
app.MapKubernetesHealthChecks(o => o.CacheDuration = TimeSpan.FromSeconds(2)); // TimeSpan.Zero turns it off
```

## License

MIT
