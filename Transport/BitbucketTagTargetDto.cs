using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents the target reference of a Bitbucket tag.
/// </summary>
internal sealed class BitbucketTagTargetDto
{
    /// <summary>
    /// Gets or sets the target commit hash.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}
