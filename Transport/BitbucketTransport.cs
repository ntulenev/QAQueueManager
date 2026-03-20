using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Transport;

internal sealed class BitbucketTransport
{
    public BitbucketTransport(HttpClient httpClient, IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _retryCount = options.Value.RetryCount;
    }

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
