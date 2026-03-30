namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents an intermediate merged issue entry before final row grouping.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="Repository">The repository identity.</param>
/// <param name="PullRequest">The normalized merged pull request.</param>
internal sealed record PendingMergedIssue(
    QaIssue Issue,
    RepositoryRef Repository,
    QaMergedPullRequest PullRequest)
{
    /// <summary>
    /// Creates a pending merged issue from a normalized Bitbucket pull request.
    /// </summary>
    /// <param name="issue">The Jira issue.</param>
    /// <param name="repository">The repository identity.</param>
    /// <param name="pullRequest">The normalized Bitbucket pull request.</param>
    /// <param name="version">The resolved artifact version.</param>
    /// <returns>The pending merged issue.</returns>
    public static PendingMergedIssue Create(
        QaIssue issue,
        RepositoryRef repository,
        BitbucketPullRequest pullRequest,
        ArtifactVersion version)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(pullRequest);

        return new PendingMergedIssue(
            issue,
            repository,
            QaMergedPullRequest.FromBitbucketPullRequest(pullRequest, version));
    }

    /// <summary>
    /// Gets the full repository name.
    /// </summary>
    public RepositoryFullName RepositoryFullName => Repository.FullName;

    /// <summary>
    /// Gets the repository slug.
    /// </summary>
    public RepositorySlug RepositorySlug => Repository.Slug;
}
