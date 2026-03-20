using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents one Jira development detail entry.
/// </summary>
internal sealed class JiraDevelopmentDetailDto
{
    /// <summary>
    /// Gets or sets the linked branches.
    /// </summary>
    [JsonPropertyName("branches")]
    public List<JiraBranchDto> Branches { get; set; } = [];

    /// <summary>
    /// Gets or sets the linked pull requests.
    /// </summary>
    [JsonPropertyName("pullRequests")]
    public List<JiraPullRequestDto> PullRequests { get; set; } = [];
}
