using System.Text.Json;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Maps Jira JSON values into display-friendly string values.
/// </summary>
internal interface IJiraObjectMapper
{
    /// <summary>
    /// Extracts a display-friendly value from a Jira JSON element.
    /// </summary>
    /// <param name="element">The element to map.</param>
    /// <returns>
    /// The mapped display value, or <see langword="null"/> when the element is null-like.
    /// </returns>
    string? ExtractDisplayValue(JsonElement element);
}
