using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a paged Bitbucket tag API response.
/// </summary>
internal sealed class BitbucketTagPageResponse
{
    /// <summary>
    /// Gets or sets the page values.
    /// </summary>
    [JsonPropertyName("values")]
    public List<BitbucketTagDto> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the next page URL.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}
