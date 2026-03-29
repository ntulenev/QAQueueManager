using System.Text.Json;

using QAQueueManager.Abstractions;

namespace QAQueueManager.API;

/// <summary>
/// Extracts display-friendly values from Jira JSON elements.
/// </summary>
internal sealed class JiraObjectMapper : IJiraObjectMapper
{
    /// <inheritdoc />
    public string? ExtractDisplayValue(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Object => ExtractObjectValue(element),
                JsonValueKind.Array => string.Join(
                    ", ",
                    element.EnumerateArray()
                        .Select(ExtractDisplayValue)
                        .Where(static value => !string.IsNullOrWhiteSpace(value))),
                JsonValueKind.Undefined =>
                    throw new NotSupportedException("Undefined JsonValueKind cannot be mapped."),
                JsonValueKind.Null =>
                    throw new NotSupportedException("Null JsonValueKind cannot be mapped."),
                _ => element.ToString()
            };
    }

    private string? ExtractObjectValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("JSON element must be an object.", nameof(element));
        }

        foreach (var propertyName in _objectDisplayPropertyOrder)
        {
            if (!element.TryGetProperty(propertyName, out var propertyValue))
            {
                continue;
            }

            var value = ExtractDisplayValue(propertyValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return element.ToString();
    }

    private static readonly IReadOnlyList<string> _objectDisplayPropertyOrder =
        ["name", "displayName", "value", "key"];
}
