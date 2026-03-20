using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Jira development branch DTO.
/// </summary>
internal sealed class JiraBranchDto
{
    /// <summary>
    /// Gets or sets the branch name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the branch repository details.
    /// </summary>
    [JsonPropertyName("repository")]
    public JiraRepositoryDto? Repository { get; set; }
}
