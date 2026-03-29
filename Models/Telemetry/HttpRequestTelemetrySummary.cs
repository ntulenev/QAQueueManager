namespace QAQueueManager.Models.Telemetry;

/// <summary>
/// Aggregated HTTP transport telemetry for the current run.
/// </summary>
public sealed record HttpRequestTelemetrySummary(
    int RequestCount,
    int RetryCount,
    long ResponseBytes,
    TimeSpan TotalDuration,
    IReadOnlyList<HttpRequestTelemetryEndpointSummary> Endpoints);
