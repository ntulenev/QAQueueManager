using System.Globalization;
using System.Text.Json;

namespace QAQueueManager.Logic;

/// <summary>
/// Extracts branch and pull request counts from Jira Development field summaries.
/// </summary>
internal static class JiraDevelopmentSummaryParser
{
    /// <summary>
    /// Parses a raw Jira Development field summary.
    /// </summary>
    /// <param name="developmentSummary">The raw development summary string.</param>
    /// <returns>The extracted count snapshot.</returns>
    public static JiraDevelopmentSummarySnapshot Parse(string? developmentSummary)
    {
        if (string.IsNullOrWhiteSpace(developmentSummary))
        {
            return default;
        }

        try
        {
            using var document = JsonDocument.Parse(developmentSummary);
            return new JiraDevelopmentSummarySnapshot(
                FindCount(document.RootElement, _pullRequestAliases),
                FindCount(document.RootElement, _branchAliases));
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static int? FindCount(JsonElement element, IReadOnlySet<string> aliases)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (aliases.Contains(property.Name))
                {
                    var count = ResolveCount(property.Value);
                    if (count.HasValue)
                    {
                        return count;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var count = FindCount(property.Value, aliases);
                if (count.HasValue)
                {
                    return count;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var count = FindCount(item, aliases);
                if (count.HasValue)
                {
                    return count;
                }
            }
        }

        return null;
    }

    private static int? ResolveCount(JsonElement element)
    {
        if (TryParseInt(element, out var directCount))
        {
            return directCount;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.GetArrayLength();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (_countAliases.Contains(property.Name) && TryParseInt(property.Value, out var propertyCount))
            {
                return propertyCount;
            }
        }

        foreach (var property in element.EnumerateObject())
        {
            var nestedCount = ResolveCount(property.Value);
            if (nestedCount.HasValue)
            {
                return nestedCount;
            }
        }

        return null;
    }

    private static bool TryParseInt(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt32(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }

    private static readonly HashSet<string> _pullRequestAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "pullrequest",
        "pullrequests",
        "pullRequest",
        "pullRequests"
    };

    private static readonly HashSet<string> _branchAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "branch",
        "branches"
    };

    private static readonly HashSet<string> _countAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "count",
        "total",
        "size",
        "overallCount"
    };
}
