using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Transport;

/// <summary>
/// Provides resilient HTTP access to Bitbucket APIs.
/// </summary>
internal sealed class BitbucketTransport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The configured HTTP client.</param>
    /// <param name="options">The Bitbucket configuration options.</param>
    public BitbucketTransport(HttpClient httpClient, IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _retryCount = options.Value.RetryCount;
    }

    /// <summary>
    /// Sends a GET request to Bitbucket and deserializes the JSON response.
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
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return await JsonSerializer.DeserializeAsync<TDto>(
                        stream,
                        _jsonOptions,
                        cancellationToken).ConfigureAwait(false);
                }

                if (ShouldRetry(attempt, response.StatusCode))
                {
                    attempt++;
                    await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Bitbucket API error {(int)response.StatusCode} {response.ReasonPhrase}. Url={url}. Body={body}",
                    null,
                    response.StatusCode);
            }
            catch (HttpRequestException) when (attempt < _retryCount)
            {
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
}
