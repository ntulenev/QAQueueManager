using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Transport;

/// <summary>
/// Provides resilient HTTP access to Jira APIs.
/// </summary>
internal sealed class JiraTransport : ResilientJsonTransport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="options">The Jira configuration options.</param>
    /// <param name="telemetryCollector">The HTTP telemetry collector.</param>
    public JiraTransport(
        HttpClient httpClient,
        IOptions<JiraOptions> options,
        IHttpRequestTelemetryCollector? telemetryCollector = null)
        : base(
            httpClient,
            options?.Value.RetryCount ?? throw new ArgumentNullException(nameof(options)),
            SOURCE_NAME,
            telemetryCollector)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    private const string SOURCE_NAME = "Jira";
}
