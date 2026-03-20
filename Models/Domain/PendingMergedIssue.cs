namespace QAQueueManager.Models.Domain;

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
    RepositorySlug RepositorySlug,
    QaMergedPullRequest PullRequest)
{
    /// <summary>
    /// Creates a pending merged issue from a normalized Bitbucket pull request.
    /// </summary>
    /// <param name="issue">The Jira issue.</param>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="repositorySlug">The repository slug.</param>
    /// <param name="pullRequest">The normalized Bitbucket pull request.</param>
    /// <param name="version">The resolved artifact version.</param>
    /// <returns>The pending merged issue.</returns>
    public static PendingMergedIssue Create(
        QaIssue issue,
        string repositoryFullName,
        RepositorySlug repositorySlug,
        BitbucketPullRequest pullRequest,
        string version)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryFullName);
        ArgumentNullException.ThrowIfNull(pullRequest);

        return new PendingMergedIssue(
            issue,
            repositoryFullName,
            repositorySlug,
            QaMergedPullRequest.FromBitbucketPullRequest(pullRequest, version));
    }
}
