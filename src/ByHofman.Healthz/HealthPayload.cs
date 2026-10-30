namespace ByHofman.Healthz;

internal sealed record HealthPayload(string Status, double TotalDurationMs, IReadOnlyList<HealthEntry> Checks);
