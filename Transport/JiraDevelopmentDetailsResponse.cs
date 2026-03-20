using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Jira development details API response.
/// </summary>
internal sealed class JiraDevelopmentDetailsResponse
{
    /// <summary>
    /// Gets or sets the development detail entries.
    /// </summary>
    [JsonPropertyName("detail")]
    public List<JiraDevelopmentDetailDto> Detail { get; set; } = [];
}
