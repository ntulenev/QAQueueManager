using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Jira development pull request DTO.
/// </summary>
internal sealed class JiraPullRequestDto
{
    /// <summary>
    /// Gets or sets the pull request identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the pull request title.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the pull request status.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the pull request URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the repository name.
    /// </summary>
    [JsonPropertyName("repositoryName")]
    public string? RepositoryName { get; set; }

    /// <summary>
    /// Gets or sets the repository URL.
    /// </summary>
    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// Gets or sets the source branch details.
    /// </summary>
    [JsonPropertyName("source")]
    public JiraPullRequestBranchDto? Source { get; set; }

    /// <summary>
    /// Gets or sets the destination branch details.
    /// </summary>
    [JsonPropertyName("destination")]
    public JiraPullRequestBranchDto? Destination { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public string? LastUpdate { get; set; }
}
