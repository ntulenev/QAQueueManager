using System.Globalization;
using System.Text.Json;

namespace QAQueueManager.API;

internal static class JsonHelpers
{
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
