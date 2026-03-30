using System.Globalization;
using System.Text.Json;

namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents the parsed state of the Jira Development field.
/// </summary>
internal readonly record struct QaIssueDevelopmentState(
    bool HasSummaryPayload,
    int? PullRequestCount,
    int? BranchCount)
{
    /// <summary>
    /// Parses the raw Jira Development field summary into a normalized state.
    /// </summary>
    /// <param name="developmentSummary">The raw development summary.</param>
    /// <returns>The parsed development state.</returns>
    public static QaIssueDevelopmentState Parse(string? developmentSummary)
    {
        if (string.IsNullOrWhiteSpace(developmentSummary))
        {
            return default;
        }

        var trimmedSummary = developmentSummary.Trim();
        if (trimmedSummary == "{}")
        {
            return new QaIssueDevelopmentState(
                HasSummaryPayload: false,
                PullRequestCount: 0,
                BranchCount: 0);
        }

        try
        {
            using var document = JsonDocument.Parse(trimmedSummary);
            return new QaIssueDevelopmentState(
                HasSummaryPayload: true,
                PullRequestCount: FindCount(document.RootElement, _pullRequestAliases),
                BranchCount: FindCount(document.RootElement, _branchAliases));
        }
        catch (JsonException)
        {
            return new QaIssueDevelopmentState(
                HasSummaryPayload: true,
                PullRequestCount: null,
                BranchCount: null);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the issue should be treated as code-linked.
    /// </summary>
    public bool HasCode => HasSummaryPayload && !HasKnownNoDevelopment;

    /// <summary>
    /// Gets a value indicating whether the summary confidently reports no linked pull requests or branches.
    /// </summary>
    public bool HasKnownNoDevelopment => PullRequestCount == 0 && BranchCount == 0;

    /// <summary>
    /// Gets a value indicating whether the summary confidently reports no linked pull requests.
    /// </summary>
    public bool HasNoPullRequests => PullRequestCount == 0;

    /// <summary>
    /// Gets a value indicating whether the summary confidently reports no linked branches.
    /// </summary>
    public bool HasNoBranches => BranchCount == 0;

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
