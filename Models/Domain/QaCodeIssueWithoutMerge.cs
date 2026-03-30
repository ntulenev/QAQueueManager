namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a code-linked Jira issue that does not have a merge into the target branch.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="Repository">The repository identity.</param>
/// <param name="PullRequests">The related pull requests.</param>
/// <param name="BranchNames">The related branch names.</param>
/// <param name="HasDuplicateIssue">Whether the issue appears multiple times in the report.</param>
internal sealed record QaCodeIssueWithoutMerge(
    QaIssue Issue,
    RepositoryRef Repository,
    IReadOnlyList<JiraPullRequestLink> PullRequests,
    IReadOnlyList<BranchName> BranchNames,
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
