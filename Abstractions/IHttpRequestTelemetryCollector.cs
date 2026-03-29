using QAQueueManager.Models.Telemetry;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Collects aggregated HTTP request telemetry for the current application run.
/// </summary>
internal interface IHttpRequestTelemetryCollector
{
    /// <summary>
    /// Resets the collected telemetry state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Records a completed HTTP request attempt.
    /// </summary>
    /// <param name="source">The logical source system, such as Jira or Bitbucket.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The request URL.</param>
    /// <param name="duration">The request duration.</param>
    /// <param name="responseBytes">The downloaded response size in bytes.</param>
    /// <param name="isRetry">Whether the attempt was a retry.</param>
    void Record(string source, string method, Uri url, TimeSpan duration, int responseBytes, bool isRetry);

    /// <summary>
    /// Builds a snapshot of the collected telemetry.
    /// </summary>
    /// <returns>The aggregated telemetry summary.</returns>
    HttpRequestTelemetrySummary GetSummary();
}
