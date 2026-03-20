using System.Globalization;
using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

/// <summary>
/// Loads Jira issues for the configured QA queue and maps them to domain models.
/// </summary>
internal sealed class JiraIssueSearchClient : IJiraIssueSearchClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueSearchClient"/> class.
    /// </summary>
    /// <param name="transport">The Jira transport.</param>
    /// <param name="options">The Jira configuration options.</param>
    public JiraIssueSearchClient(JiraTransport transport, IOptions<JiraOptions> options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);

        _transport = transport;
        _options = options.Value;
    }

    /// <summary>
    /// Loads Jira issues matching the configured JQL query.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mapped Jira issues.</returns>
    public async Task<IReadOnlyList<QaIssue>> SearchIssuesAsync(CancellationToken cancellationToken)
    {
        var developmentApiField = await ResolveConfiguredFieldAsync(_options.DevelopmentField, cancellationToken).ConfigureAwait(false);
        var teamApiFields = await ResolveOptionalConfiguredFieldsAsync(_options.TeamField, cancellationToken).ConfigureAwait(false);
        var requestedFieldList = new List<string> { "summary", "status", "updated", developmentApiField };
        if (teamApiFields.Count > 0)
        {
            requestedFieldList.AddRange(teamApiFields);
        }

        var requestedFields = string.Join(",", requestedFieldList.Distinct(StringComparer.OrdinalIgnoreCase));
        var issues = new List<QaIssue>();
        var pageSize = Math.Clamp(_options.MaxResultsPerPage, 1, 100);
        string? nextPageToken = null;

        try
        {
            while (true)
            {
                var url =
                    $"rest/api/3/search/jql?jql={Uri.EscapeDataString(_options.Jql)}" +
                    $"&fields={Uri.EscapeDataString(requestedFields)}" +
                    $"&maxResults={pageSize}";

                if (!string.IsNullOrWhiteSpace(nextPageToken))
                {
                    url += $"&nextPageToken={Uri.EscapeDataString(nextPageToken)}";
                }

                var page = await _transport
                    .GetAsync<JiraSearchResponse>(new Uri(url, UriKind.Relative), cancellationToken)
                    .ConfigureAwait(false)
                    ?? new JiraSearchResponse();

                issues.AddRange(MapIssues(page.Issues, developmentApiField, teamApiFields));

                nextPageToken = page.NextPageToken;
                if (page.Issues.Count == 0 || page.IsLast || string.IsNullOrWhiteSpace(nextPageToken))
                {
                    break;
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var startAt = 0;
            while (true)
            {
                var url =
                    $"rest/api/3/search?jql={Uri.EscapeDataString(_options.Jql)}" +
                    $"&fields={Uri.EscapeDataString(requestedFields)}" +
                    $"&startAt={startAt}&maxResults={pageSize}";

                var page = await _transport
                    .GetAsync<JiraSearchResponse>(new Uri(url, UriKind.Relative), cancellationToken)
                    .ConfigureAwait(false)
                    ?? new JiraSearchResponse();

                issues.AddRange(MapIssues(page.Issues, developmentApiField, teamApiFields));

                if (page.Issues.Count == 0)
                {
                    break;
                }

                startAt += page.Issues.Count;
                var total = page.Total > 0 ? page.Total : startAt;
                if (startAt >= total)
                {
                    break;
                }
            }
        }

        return issues;
    }

    private async Task<IReadOnlyList<string>> ResolveOptionalConfiguredFieldsAsync(
        string? configuredFields,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredFields))
        {
            return [];
        }

        var result = new List<string>();
        var aliases = configuredFields
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var alias in aliases)
        {
            var resolvedFields = await ResolveConfiguredFieldsAsync(alias, cancellationToken).ConfigureAwait(false);
            foreach (var field in resolvedFields)
            {
                if (!result.Contains(field, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(field);
                }
            }
        }

        return result;
    }

    private async Task<string> ResolveConfiguredFieldAsync(
        string configuredField,
        CancellationToken cancellationToken)
    {
        var fields = await ResolveConfiguredFieldsAsync(configuredField, cancellationToken).ConfigureAwait(false);
        return fields.Count == 0
            ? throw new InvalidOperationException(
                $"Unable to resolve Jira field '{configuredField}'.")
            : fields[0];
    }

    private async Task<IReadOnlyList<string>> ResolveConfiguredFieldsAsync(
        string configuredField,
        CancellationToken cancellationToken)
    {
        var alias = SimplifyAlias(configuredField);
        if (_resolvedFields.TryGetValue(alias, out var cachedFields))
        {
            return cachedFields;
        }

        if (configuredField.StartsWith("customfield_", StringComparison.OrdinalIgnoreCase))
        {
            var directField = configuredField.Trim();
            _resolvedFields[alias] = [directField];
            return _resolvedFields[alias];
        }

        var lookup = await GetFieldLookupAsync(cancellationToken).ConfigureAwait(false);
        if (!lookup.TryGetValue(alias, out var apiFields) || apiFields.Count == 0)
        {
            throw new InvalidOperationException(
                $"Unable to resolve Jira field '{configuredField}'.");
        }

        _resolvedFields[alias] = apiFields;
        return apiFields;
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> GetFieldLookupAsync(CancellationToken cancellationToken)
    {
        if (_fieldLookup is not null)
        {
            return _fieldLookup;
        }

        var fields = await _transport
            .GetAsync<List<JiraFieldDefinitionResponse>>(new Uri("rest/api/3/field", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        _fieldLookup = BuildFieldLookup(fields);
        return _fieldLookup;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildFieldLookup(IEnumerable<JiraFieldDefinitionResponse> fields)
    {
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

    private static void AddAlias(Dictionary<string, List<string>> lookup, string? alias, string apiField)
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

    private static string SimplifyAlias(string alias)
    {
        var value = alias.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1].Trim();
        }

        return value;
    }

    private static List<QaIssue> MapIssues(
        List<JiraIssueResponse> issues,
        string developmentApiField,
        IReadOnlyList<string> teamApiFields)
    {
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
                JsonValueKind.Undefined => throw new NotImplementedException(),
                JsonValueKind.Null => throw new NotImplementedException(),
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

    private readonly JiraTransport _transport;
    private readonly JiraOptions _options;
    private Dictionary<string, IReadOnlyList<string>>? _fieldLookup;
    private readonly Dictionary<string, IReadOnlyList<string>> _resolvedFields = new(StringComparer.OrdinalIgnoreCase);
}
