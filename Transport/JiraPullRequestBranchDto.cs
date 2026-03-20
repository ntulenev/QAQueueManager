using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents branch information inside a Jira pull request DTO.
/// </summary>
internal sealed class JiraPullRequestBranchDto
{
    /// <summary>
    /// Gets or sets the branch name.
    /// </summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
}
