namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents one merged report row for a concrete issue and artifact version.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="Repository">The repository identity.</param>
/// <param name="Version">The artifact version.</param>
/// <param name="PullRequests">The pull requests associated with this version.</param>
/// <param name="HasDuplicateIssue">Whether the issue appears multiple times in the report.</param>
internal sealed record QaMergedIssueVersionRow(
    QaIssue Issue,
    RepositoryRef Repository,
    ArtifactVersion Version,
    IReadOnlyList<QaMergedPullRequest> PullRequests,
    bool HasDuplicateIssue)
{
    /// <summary>
    /// Gets the full repository name.
    /// </summary>
    public RepositoryFullName RepositoryFullName => Repository.FullName;

    /// <summary>
    /// Gets the repository slug.
    /// </summary>
    public RepositorySlug RepositorySlug => Repository.Slug;
}
