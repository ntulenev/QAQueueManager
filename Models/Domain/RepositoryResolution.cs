namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents the repository-specific resolution result for a code-linked issue.
/// </summary>
/// <param name="Repository">The repository identity.</param>
/// <param name="WithoutMerge">The no-merge payload when the issue is not merged into the target branch.</param>
/// <param name="Merged">The merged payload when the issue is merged into the target branch.</param>
internal sealed record RepositoryResolution(
    RepositoryRef Repository,
    IssueWithoutMergeData? WithoutMerge,
    MergedIssueData? Merged)
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
