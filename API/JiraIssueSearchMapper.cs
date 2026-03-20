using System.Globalization;
using System.Text.Json;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

/// <summary>
/// Maps Jira search DTOs and field metadata into domain models.
/// </summary>
internal sealed class JiraIssueSearchMapper : IJiraIssueSearchMapper
{
    /// <inheritdoc />
    public string SimplifyAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var value = alias.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1].Trim();
        }

        return value;
    }

    /// <inheritdoc />
    public Dictionary<string, IReadOnlyList<string>> BuildFieldLookup(IEnumerable<JiraFieldDefinitionResponse> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            var apiField = !string.IsNullOrWhiteSpace(field.Key) ? field.Key.Trim() : field.Id?.Trim();
            if (string.IsNullOrWhiteSpace(apiField))
            {
                continue;
            }

            AddAlias(result, field.Id, apiField);
            AddAlias(result, field.Key, apiField);
            AddAlias(result, field.Name, apiField);

            foreach (var clauseName in field.ClauseNames)
            {
                AddAlias(result, clauseName, apiField);
            }
        }

        return result.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public List<QaIssue> MapIssues(
        List<JiraIssueResponse> issues,
        string developmentApiField,
        IReadOnlyList<string> teamApiFields)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentException.ThrowIfNullOrWhiteSpace(developmentApiField);
        ArgumentNullException.ThrowIfNull(teamApiFields);

        var result = new List<QaIssue>(issues.Count);

        foreach (var issue in issues)
        {
            if (!long.TryParse(issue.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var issueId) ||
                issueId <= 0 ||
                string.IsNullOrWhiteSpace(issue.Key))
            {
                continue;
            }

            var values = issue.Fields?.Values ?? [];
            _ = values.TryGetValue("summary", out var summaryElement);
            _ = values.TryGetValue("status", out var statusElement);
            _ = values.TryGetValue("updated", out var updatedElement);
            _ = values.TryGetValue(developmentApiField, out var developmentElement);

            var summary = ExtractDisplayValue(summaryElement) ?? "-";
            var status = ExtractDisplayValue(statusElement) ?? "-";
            var development = ExtractDisplayValue(developmentElement) ?? "{}";
            var teams = ExtractTeams(values, teamApiFields);
            var updatedAt = TryParseDate(updatedElement);

            result.Add(new QaIssue(new JiraIssueId(issueId), issue.Key.Trim(), summary, status, development, teams, updatedAt));
        }

        return result;
    }

    private void AddAlias(Dictionary<string, List<string>> lookup, string? alias, string apiField)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        var normalizedAlias = SimplifyAlias(alias);
        if (!lookup.TryGetValue(normalizedAlias, out var fields))
        {
            fields = [];
            lookup[normalizedAlias] = fields;
        }

        if (!fields.Contains(apiField, StringComparer.OrdinalIgnoreCase))
        {
            fields.Add(apiField);
        }
    }

    private static List<string> ExtractTeams(
        Dictionary<string, JsonElement> values,
        IReadOnlyList<string> teamApiFields)
    {
        if (teamApiFields.Count == 0)
        {
            return [];
        }

        var teams = new List<string>();
        foreach (var teamApiField in teamApiFields)
        {
            if (!values.TryGetValue(teamApiField, out var teamElement))
            {
                continue;
            }

            var extracted = ExtractDisplayValues(teamElement);
            foreach (var team in extracted)
            {
                if (!teams.Contains(team, StringComparer.OrdinalIgnoreCase))
                {
                    teams.Add(team);
                }
            }
        }

        return teams;
    }

    private static string? ExtractDisplayValue(JsonElement element)
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
                JsonValueKind.Undefined => throw new NotSupportedException("Undefined JsonValueKind cannot be mapped."),
                JsonValueKind.Null => throw new NotSupportedException("Null JsonValueKind cannot be mapped."),
                _ => element.ToString()
            };
    }

    private static IReadOnlyList<string> ExtractDisplayValues(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return [.. element.EnumerateArray()
                .SelectMany(ExtractDisplayValues)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        var value = ExtractDisplayValue(element);
        return string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
    }

    private static string? ExtractObjectValue(JsonElement element)
    {
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

    private static DateTimeOffset? TryParseDate(JsonElement element)
    {
        var value = ExtractDisplayValue(element);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static readonly IReadOnlyList<string> _objectDisplayPropertyOrder =
        ["name", "displayName", "value", "key"];
}
