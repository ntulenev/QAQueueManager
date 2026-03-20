using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Provides access to Jira development links associated with issues.
/// </summary>
internal interface IJiraDevelopmentClient
{
    /// <summary>
    /// Loads pull requests linked to a Jira issue.
    /// </summary>
    /// <param name="issueId">The Jira issue identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The linked pull requests.</returns>
    Task<IReadOnlyList<JiraPullRequestLink>> GetPullRequestsAsync(JiraIssueId issueId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads branches linked to a Jira issue.
    /// </summary>
    /// <param name="issueId">The Jira issue identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The linked branches.</returns>
    Task<IReadOnlyList<JiraBranchLink>> GetBranchesAsync(JiraIssueId issueId, CancellationToken cancellationToken);
}
