using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a hyperlink DTO returned by Bitbucket.
/// </summary>
internal sealed class BitbucketHrefDto
{
    /// <summary>
    /// Gets or sets the hyperlink URL.
    /// </summary>
    [JsonPropertyName("href")]
    public string? Href { get; set; }
}
