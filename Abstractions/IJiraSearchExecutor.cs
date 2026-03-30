using QAQueueManager.Transport;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Executes Jira issue searches and handles endpoint fallback and paging.
/// </summary>
internal interface IJiraSearchExecutor
{
    /// <summary>
    /// Executes the configured Jira JQL search and returns the collected issue DTOs.
    /// </summary>
    /// <param name="jql">The Jira query.</param>
    /// <param name="requestedFields">The requested Jira field names.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The collected Jira issue DTOs.</returns>
    Task<IReadOnlyList<JiraIssueResponse>> SearchIssuesAsync(
        string jql,
        IReadOnlyList<string> requestedFields,
        int pageSize,
        CancellationToken cancellationToken);
}
