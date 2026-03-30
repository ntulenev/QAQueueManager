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
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueSearchMapper"/> class.
    /// </summary>
    /// <param name="objectMapper">The Jira object mapper used for display value extraction.</param>
    public JiraIssueSearchMapper(IJiraObjectMapper objectMapper)
    {
        _objectMapper = objectMapper ?? throw new ArgumentNullException(nameof(objectMapper));
    }

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
    public Dictionary<string, IReadOnlyList<string>> BuildFieldLookup(
        IEnumerable<JiraFieldDefinitionResponse> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            var apiField = !string.IsNullOrWhiteSpace(field.Key)
                ? field.Key.Trim()
                : field.Id?.Trim();
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
            if (!long.TryParse(
                    issue.Id,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var issueId) ||
                issueId <= 0 ||
                string.IsNullOrWhiteSpace(issue.Key))
            {
                continue;
            }

            var values = issue.Fields?.Values ?? [];
            _ = values.TryGetValue("summary", out var summaryElement);
            _ = values.TryGetValue("status", out var statusElement);
            _ = values.TryGetValue("assignee", out var assigneeElement);
            _ = values.TryGetValue("updated", out var updatedElement);
            _ = values.TryGetValue(developmentApiField, out var developmentElement);

            var summary = _objectMapper.ExtractDisplayValue(summaryElement) ?? "-";
            var status =
                _objectMapper.ExtractDisplayValue(statusElement) ?? JiraIssueStatus.Unknown.Value;
            var assignee = ExtractAssignee(assigneeElement);
            var development = _objectMapper.ExtractDisplayValue(developmentElement) ?? "{}";
            var teams = ExtractTeams(values, teamApiFields);
            var updatedAt = updatedElement.TryParseDate(_objectMapper.ExtractDisplayValue);

            result.Add(new QaIssue(
                new JiraIssueId(issueId),
                new JiraIssueKey(issue.Key),
                summary,
                new JiraIssueStatus(status),
                assignee,
                development,
                teams,
                updatedAt));
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

    private List<TeamName> ExtractTeams(
        Dictionary<string, JsonElement> values,
        IReadOnlyList<string> teamApiFields)
    {
        if (teamApiFields.Count == 0)
        {
            return [];
        }

        var teams = new List<TeamName>();
        foreach (var teamApiField in teamApiFields)
        {
            if (!values.TryGetValue(teamApiField, out var teamElement))
            {
                continue;
            }

            var extracted = ExtractDisplayValues(teamElement);
            foreach (var team in extracted)
            {
                if (!teams.Any(existing =>
                        string.Equals(existing.Value, team, StringComparison.OrdinalIgnoreCase)))
                {
                    teams.Add(new TeamName(team));
                }
            }
        }

        return teams;
    }

    private IReadOnlyList<string> ExtractDisplayValues(JsonElement element)
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

        var value = _objectMapper.ExtractDisplayValue(element);
        return string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
    }

    private string ExtractAssignee(JsonElement assigneeElement)
    {
        if (assigneeElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return UNASSIGNED_ASSIGNEE;
        }

        if (assigneeElement.ValueKind == JsonValueKind.Object &&
            assigneeElement.TryGetProperty("displayName", out var displayNameElement))
        {
            var displayName = _objectMapper.ExtractDisplayValue(displayNameElement);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }
        }

        var assignee = _objectMapper.ExtractDisplayValue(assigneeElement);
        return string.IsNullOrWhiteSpace(assignee) ? UNASSIGNED_ASSIGNEE : assignee.Trim();
    }

    private readonly IJiraObjectMapper _objectMapper;
    private const string UNASSIGNED_ASSIGNEE = "-";
}
