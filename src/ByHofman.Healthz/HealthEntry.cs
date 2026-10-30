namespace ByHofman.Healthz;

internal sealed record HealthEntry(string Name, string Status, string? Description, double DurationMs);
