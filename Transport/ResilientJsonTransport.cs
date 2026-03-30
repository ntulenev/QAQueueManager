using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

using QAQueueManager.Abstractions;

namespace QAQueueManager.Transport;

/// <summary>
/// Provides resilient HTTP access for JSON-based APIs with shared retry and telemetry behavior.
/// </summary>
internal abstract class ResilientJsonTransport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientJsonTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="retryCount">The maximum retry count.</param>
    /// <param name="sourceName">The telemetry source name.</param>
    /// <param name="telemetryCollector">The HTTP telemetry collector.</param>
    protected ResilientJsonTransport(
        HttpClient httpClient,
        int retryCount,
        string sourceName,
        IHttpRequestTelemetryCollector? telemetryCollector = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        _httpClient = httpClient;
        _retryCount = retryCount;
        _sourceName = sourceName;
        _telemetryCollector = telemetryCollector ?? new HttpRequestTelemetryCollector();
    }

    /// <summary>
    /// Sends a GET request and deserializes the JSON response.
    /// </summary>
    /// <typeparam name="TDto">The target DTO type.</typeparam>
    /// <param name="url">The relative or absolute request URL.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The deserialized response payload, or <see langword="null"/> when the body is empty.</returns>
    public async Task<TDto?> GetAsync<TDto>(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        var attempt = 0;

        while (true)
        {
            var requestRecorded = false;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                _telemetryCollector.Record(
                    source: _sourceName,
                    method: HttpMethod.Get.Method,
                    url,
                    stopwatch.Elapsed,
                    responseBytes.Length,
                    isRetry: attempt > 0);
                requestRecorded = true;

                if (response.IsSuccessStatusCode)
                {
                    return responseBytes.Length == 0
                        ? default
                        : JsonSerializer.Deserialize<TDto>(responseBytes, _jsonOptions);
                }

                if (ShouldRetry(attempt, response.StatusCode))
                {
                    attempt++;
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var body = responseBytes.Length == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(responseBytes);
                throw new HttpRequestException(
                    $"{_sourceName} API error {(int)response.StatusCode} {response.ReasonPhrase}. Url={url}. Body={body}",
                    null,
                    response.StatusCode);
            }
            catch (HttpRequestException) when (attempt < _retryCount)
            {
                if (!requestRecorded)
                {
                    stopwatch.Stop();
                    _telemetryCollector.Record(
                        source: _sourceName,
                        method: HttpMethod.Get.Method,
                        url,
                        stopwatch.Elapsed,
                        responseBytes: 0,
                        isRetry: attempt > 0);
                }

                attempt++;
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool ShouldRetry(int attempt, HttpStatusCode statusCode)
        => attempt < _retryCount &&
           (statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500);

    private static TimeSpan GetRetryDelay(int attempt) => TimeSpan.FromMilliseconds(250 * attempt);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly int _retryCount;
    private readonly string _sourceName;
    private readonly IHttpRequestTelemetryCollector _telemetryCollector;
}
