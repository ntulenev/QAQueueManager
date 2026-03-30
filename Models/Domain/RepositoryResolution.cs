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

    /// <summary>
    /// Creates the fallback resolution used when development data is explicitly absent.
    /// </summary>
    /// <returns>The unknown no-merge resolution.</returns>
    public static RepositoryResolution CreateUnknownWithoutMerge()
    {
        return new RepositoryResolution(
            RepositoryRef.Unknown,
            IssueWithoutMergeData.Create([], []),
            null);
    }

    /// <summary>
    /// Creates a no-merge resolution for the supplied repository.
    /// </summary>
    /// <param name="repository">The repository identity.</param>
    /// <param name="pullRequests">The related pull requests.</param>
    /// <param name="branchNames">The related branch names.</param>
    /// <returns>The no-merge resolution.</returns>
    public static RepositoryResolution CreateWithoutMerge(
        RepositoryRef repository,
        IReadOnlyList<JiraPullRequestLink> pullRequests,
        IReadOnlyList<BranchName> branchNames)
    {
        ArgumentNullException.ThrowIfNull(pullRequests);
        ArgumentNullException.ThrowIfNull(branchNames);

        return new RepositoryResolution(
            repository,
            IssueWithoutMergeData.Create(pullRequests, branchNames),
            null);
    }

    /// <summary>
    /// Creates a merged resolution for the supplied repository.
    /// </summary>
    /// <param name="repository">The repository identity.</param>
    /// <param name="pullRequest">The merged pull request.</param>
    /// <param name="version">The resolved artifact version.</param>
    /// <returns>The merged resolution.</returns>
    public static RepositoryResolution CreateMerged(
        RepositoryRef repository,
        BitbucketPullRequest pullRequest,
        ArtifactVersion version)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        return new RepositoryResolution(
            repository,
            null,
            MergedIssueData.Create(pullRequest, version));
    }

    /// <summary>
    /// Creates a merged fallback resolution from Jira development metadata when Bitbucket details are unavailable.
    /// </summary>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="repository">The repository identity.</param>
    /// <param name="candidate">The Jira pull request candidate.</param>
    /// <returns>The merged fallback resolution.</returns>
    public static RepositoryResolution CreateMergedFallback(
        RepositoryFullName repositoryFullName,
        RepositoryRef repository,
        JiraPullRequestLink candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return CreateMerged(
            repository,
            BitbucketPullRequest.CreateMergedFallback(candidate, repositoryFullName, repository),
            ArtifactVersion.NotFound);
    }
}
