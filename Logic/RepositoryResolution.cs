using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Represents the repository-specific resolution result for a code-linked issue.
/// </summary>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositorySlug">The repository slug.</param>
/// <param name="WithoutMerge">The no-merge payload when the issue is not merged into the target branch.</param>
/// <param name="Merged">The merged payload when the issue is merged into the target branch.</param>
internal sealed record RepositoryResolution(
    string RepositoryFullName,
    RepositorySlug RepositorySlug,
    IssueWithoutMergeData? WithoutMerge,
    MergedIssueData? Merged);
