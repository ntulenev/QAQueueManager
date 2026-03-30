using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.API;

/// <summary>
/// Loads Jira issues for the configured QA queue and maps them to domain models.
/// </summary>
internal sealed class JiraIssueSearchClient : IJiraIssueSearchClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueSearchClient"/> class.
    /// </summary>
    /// <param name="fieldResolver">The Jira field resolver.</param>
    /// <param name="searchExecutor">The Jira search executor.</param>
    /// <param name="mapper">The Jira issue search mapper.</param>
    /// <param name="options">The Jira configuration options.</param>
    public JiraIssueSearchClient(
        IJiraFieldResolver fieldResolver,
        IJiraSearchExecutor searchExecutor,
        IJiraIssueSearchMapper mapper,
        IOptions<JiraOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fieldResolver);
        ArgumentNullException.ThrowIfNull(searchExecutor);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(options);

        _fieldResolver = fieldResolver;
        _searchExecutor = searchExecutor;
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
        var developmentApiField = await _fieldResolver
            .ResolveRequiredFieldAsync(_options.DevelopmentField, cancellationToken)
            .ConfigureAwait(false);
        var teamApiFields = await _fieldResolver
            .ResolveOptionalFieldsAsync(_options.TeamField, cancellationToken)
            .ConfigureAwait(false);
        var requestedFieldList = new List<string> { "summary", "status", "assignee", "updated", developmentApiField };
        if (teamApiFields.Count > 0)
        {
            requestedFieldList.AddRange(teamApiFields);
        }

        var issueDtos = await _searchExecutor
            .SearchIssuesAsync(_options.Jql, requestedFieldList, _options.MaxResultsPerPage, cancellationToken)
            .ConfigureAwait(false);

        return _mapper.MapIssues([.. issueDtos], developmentApiField, teamApiFields);
    }

    private readonly IJiraFieldResolver _fieldResolver;
    private readonly IJiraSearchExecutor _searchExecutor;
    private readonly IJiraIssueSearchMapper _mapper;
    private readonly JiraOptions _options;
}
