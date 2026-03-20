using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Searches Jira issues for the configured QA queue.
/// </summary>
internal interface IJiraIssueSearchClient
{
    /// <summary>
    /// Loads Jira issues that match the configured JQL filter.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The list of mapped Jira issues.</returns>
    Task<IReadOnlyList<QaIssue>> SearchIssuesAsync(CancellationToken cancellationToken);
}
