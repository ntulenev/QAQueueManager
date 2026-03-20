using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Bitbucket tag DTO.
/// </summary>
internal sealed class BitbucketTagDto
{
    /// <summary>
    /// Gets or sets the tag name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the tag creation date.
    /// </summary>
    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }

    /// <summary>
    /// Gets or sets the tag target reference.
    /// </summary>
    [JsonPropertyName("target")]
    public BitbucketTagTargetDto? Target { get; set; }
}
