using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Bitbucket branch DTO.
/// </summary>
internal sealed class BitbucketBranchDto
{
    /// <summary>
    /// Gets or sets the branch name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
