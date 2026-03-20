using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Jira issue DTO returned by search.
/// </summary>
internal sealed class JiraIssueResponse
{
    /// <summary>
    /// Gets or sets the Jira issue identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the Jira issue key.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the field payload.
    /// </summary>
    [JsonPropertyName("fields")]
    public JiraIssueFieldsResponse? Fields { get; set; }
}
