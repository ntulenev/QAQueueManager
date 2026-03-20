using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Represents an intermediate merged issue entry before final row grouping.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositorySlug">The repository slug.</param>
/// <param name="PullRequest">The normalized merged pull request.</param>
internal sealed record PendingMergedIssue(
    QaIssue Issue,
    string RepositoryFullName,
    string RepositorySlug,
    QaMergedPullRequest PullRequest);
