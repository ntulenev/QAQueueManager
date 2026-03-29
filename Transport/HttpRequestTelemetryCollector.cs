using QAQueueManager.Abstractions;
using QAQueueManager.Models.Telemetry;

namespace QAQueueManager.Transport;

/// <summary>
/// In-memory collector for HTTP request telemetry.
/// </summary>
internal sealed class HttpRequestTelemetryCollector : IHttpRequestTelemetryCollector
{
    /// <inheritdoc />
    public void Reset()
    {
        lock (_sync)
        {
            _requestCount = 0;
            _retryCount = 0;
            _responseBytes = 0;
            _totalDuration = TimeSpan.Zero;
            _endpointMetrics.Clear();
        }
    }

    /// <inheritdoc />
    public void Record(string source, string method, Uri url, TimeSpan duration, int responseBytes, bool isRetry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(url);

        var normalizedSource = source.Trim();
        var normalizedMethod = method.ToUpperInvariant();
        var endpoint = NormalizeEndpoint(url);
        var key = $"{normalizedSource}|{normalizedMethod}|{endpoint}";

        lock (_sync)
        {
            _requestCount++;
            if (isRetry)
            {
                _retryCount++;
            }

            _responseBytes += responseBytes;
            _totalDuration += duration;

            if (!_endpointMetrics.TryGetValue(key, out var metric))
            {
                metric = new EndpointMetrics(normalizedSource, normalizedMethod, endpoint);
                _endpointMetrics[key] = metric;
            }

            metric.RequestCount++;
            if (isRetry)
            {
                metric.RetryCount++;
            }

            metric.ResponseBytes += responseBytes;
            metric.TotalDuration += duration;
            if (duration > metric.MaxDuration)
            {
                metric.MaxDuration = duration;
            }
        }
    }

    /// <inheritdoc />
    public HttpRequestTelemetrySummary GetSummary()
    {
        lock (_sync)
        {
            return new HttpRequestTelemetrySummary(
                _requestCount,
                _retryCount,
                _responseBytes,
                _totalDuration,
                [.. _endpointMetrics.Values
                    .Select(static metric => new HttpRequestTelemetryEndpointSummary(
                        metric.Source,
                        metric.Method,
                        metric.Endpoint,
                        metric.RequestCount,
                        metric.RetryCount,
                        metric.ResponseBytes,
                        metric.TotalDuration,
                        metric.MaxDuration))
                    .OrderByDescending(static metric => metric.TotalDuration)
                    .ThenByDescending(static metric => metric.RequestCount)
                    .ThenBy(static metric => metric.Source, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static metric => metric.Endpoint, StringComparer.OrdinalIgnoreCase)]);
        }
    }

    private static string NormalizeEndpoint(Uri url)
    {
        var path = url.IsAbsoluteUri
            ? url.AbsolutePath
            : url.OriginalString.Split('?', 2)[0].Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path[0] == '/' ? path : $"/{path}";
    }

    private sealed class EndpointMetrics
    {
        public EndpointMetrics(string source, string method, string endpoint)
        {
            Source = source;
            Method = method;
            Endpoint = endpoint;
        }

        public string Source { get; }

        public string Method { get; }

        public string Endpoint { get; }

        public int RequestCount { get; set; }

        public int RetryCount { get; set; }

        public long ResponseBytes { get; set; }

        public TimeSpan TotalDuration { get; set; }

        public TimeSpan MaxDuration { get; set; }
    }

    private readonly Lock _sync = new();
    private readonly Dictionary<string, EndpointMetrics> _endpointMetrics =
        new(StringComparer.OrdinalIgnoreCase);
    private int _requestCount;
    private int _retryCount;
    private long _responseBytes;
    private TimeSpan _totalDuration;
}
