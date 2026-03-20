using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Bitbucket repository DTO.
/// </summary>
internal sealed class BitbucketRepositoryDto
{
    /// <summary>
    /// Gets or sets the full repository name.
    /// </summary>
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the repository.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
