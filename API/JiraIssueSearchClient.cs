using System.Net;

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
    /// <param name="mapper">The Jira issue search mapper.</param>
    /// <param name="options">The Jira configuration options.</param>
    public JiraIssueSearchClient(
        JiraTransport transport,
        IJiraIssueSearchMapper mapper,
        IOptions<JiraOptions> options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(options);

        _transport = transport;
        _mapper = mapper;
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
        var requestedFieldList = new List<string> { "summary", "status", "assignee", "updated", developmentApiField };
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

                issues.AddRange(_mapper.MapIssues(page.Issues, developmentApiField, teamApiFields));

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

                issues.AddRange(_mapper.MapIssues(page.Issues, developmentApiField, teamApiFields));

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
            ? throw new InvalidOperationException($"Unable to resolve Jira field '{configuredField}'.")
            : fields[0];
    }

    private async Task<IReadOnlyList<string>> ResolveConfiguredFieldsAsync(
        string configuredField,
        CancellationToken cancellationToken)
    {
        var alias = _mapper.SimplifyAlias(configuredField);
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
            throw new InvalidOperationException($"Unable to resolve Jira field '{configuredField}'.");
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

        _fieldLookup = _mapper.BuildFieldLookup(fields);
        return _fieldLookup;
    }

    private readonly JiraTransport _transport;
    private readonly IJiraIssueSearchMapper _mapper;
    private readonly JiraOptions _options;
    private Dictionary<string, IReadOnlyList<string>>? _fieldLookup;
    private readonly Dictionary<string, IReadOnlyList<string>> _resolvedFields = new(StringComparer.OrdinalIgnoreCase);
}
