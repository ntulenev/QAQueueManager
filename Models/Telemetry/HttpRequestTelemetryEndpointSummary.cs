namespace QAQueueManager.Models.Telemetry;

/// <summary>
/// Aggregated HTTP transport telemetry per source and endpoint.
/// </summary>
public sealed record HttpRequestTelemetryEndpointSummary(
    string Source,
    string Method,
    string Endpoint,
    int RequestCount,
    int RetryCount,
    long ResponseBytes,
    TimeSpan TotalDuration,
    TimeSpan MaxDuration);
