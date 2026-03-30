using System.Globalization;
using System.Text.Json;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;
namespace QAQueueManager.API;

/// <summary>
/// Maps Jira search DTOs into domain models.
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

            var summary = _objectMapper.ExtractDisplayValue(summaryElement);
            var status = _objectMapper.ExtractDisplayValue(statusElement);
            var assignee = ExtractAssignee(assigneeElement);
            var development = _objectMapper.ExtractDisplayValue(developmentElement);
            var teams = ExtractTeams(values, teamApiFields);
            var updatedAt = updatedElement.TryParseDate(_objectMapper.ExtractDisplayValue);

            result.Add(QaIssue.Create(
                new JiraIssueId(issueId),
                new JiraIssueKey(issue.Key),
                summary,
                status,
                assignee,
                development,
                teams,
                updatedAt));
        }

        return result;
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
            return string.Empty;
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
        return string.IsNullOrWhiteSpace(assignee) ? string.Empty : assignee.Trim();
    }

    private readonly IJiraObjectMapper _objectMapper;
}
