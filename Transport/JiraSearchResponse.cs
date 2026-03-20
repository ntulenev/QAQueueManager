using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Jira search API response.
/// </summary>
internal sealed class JiraSearchResponse
{
    /// <summary>
    /// Gets or sets the returned issues.
    /// </summary>
    [JsonPropertyName("issues")]
    public List<JiraIssueResponse> Issues { get; set; } = [];

    /// <summary>
    /// Gets or sets the cursor for the next page in cursor-based search.
    /// </summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this page is the last page.
    /// </summary>
    [JsonPropertyName("isLast")]
    public bool IsLast { get; set; }

    /// <summary>
    /// Gets or sets the total number of issues in offset-based search.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}
