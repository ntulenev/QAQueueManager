using System.Globalization;
using System.Text.Json;

namespace QAQueueManager.API;

/// <summary>
/// Provides shared helpers for working with JSON values.
/// </summary>
internal static class JsonHelpers
{
    /// <summary>
    /// Attempts to parse a date from a JSON element using the supplied display-value extractor.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="extractDisplayValue">The extractor used to convert the element to text.</param>
    /// <returns>The parsed date when conversion succeeds; otherwise, <see langword="null"/>.</returns>
    internal static DateTimeOffset? TryParseDate(
        this JsonElement element,
        Func<JsonElement, string?> extractDisplayValue)
    {
        ArgumentNullException.ThrowIfNull(extractDisplayValue);

        var value = extractDisplayValue(element);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }
}
