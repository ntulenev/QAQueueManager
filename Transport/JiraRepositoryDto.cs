using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a repository DTO inside Jira development data.
/// </summary>
internal sealed class JiraRepositoryDto
{
    /// <summary>
    /// Gets or sets the repository name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the repository URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
