using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Jira field definition DTO.
/// </summary>
internal sealed class JiraFieldDefinitionResponse
{
    /// <summary>
    /// Gets or sets the field identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the field key.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the display name of the field.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the clause names associated with the field.
    /// </summary>
    [JsonPropertyName("clauseNames")]
    public List<string> ClauseNames { get; set; } = [];
}
