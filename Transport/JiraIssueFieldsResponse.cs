using System.Text.Json;
using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents the dynamic Jira issue fields payload.
/// </summary>
internal sealed class JiraIssueFieldsResponse
{
    /// <summary>
    /// Gets or sets the raw field values keyed by Jira field id.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Values { get; set; } = [];
}
