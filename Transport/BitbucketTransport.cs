using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Transport;

/// <summary>
/// Provides resilient HTTP access to Bitbucket APIs.
/// </summary>
internal sealed class BitbucketTransport : ResilientJsonTransport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="options">The Bitbucket configuration options.</param>
    /// <param name="telemetryCollector">The HTTP telemetry collector.</param>
    public BitbucketTransport(
        HttpClient httpClient,
        IOptions<BitbucketOptions> options,
        IHttpRequestTelemetryCollector? telemetryCollector = null)
        : base(
            httpClient,
            options?.Value.RetryCount ?? throw new ArgumentNullException(nameof(options)),
            SOURCE_NAME,
            telemetryCollector)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    private const string SOURCE_NAME = "Bitbucket";
}
