using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Bitbucket commit reference DTO.
/// </summary>
internal sealed class BitbucketCommitRefDto
{
    /// <summary>
    /// Gets or sets the commit hash.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}
