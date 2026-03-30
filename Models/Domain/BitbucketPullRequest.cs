namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized Bitbucket pull request used by the report builder.
/// </summary>
/// <param name="Id">The pull request identifier.</param>
/// <param name="State">The pull request state.</param>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositoryDisplayName">The repository display name.</param>
/// <param name="RepositorySlug">The repository slug.</param>
/// <param name="SourceBranch">The source branch name.</param>
/// <param name="DestinationBranch">The destination branch name.</param>
/// <param name="HtmlUrl">The HTML URL of the pull request.</param>
/// <param name="MergeCommitHash">The merge commit hash.</param>
/// <param name="UpdatedOn">The last updated timestamp.</param>
internal sealed record BitbucketPullRequest(
    PullRequestId Id,
    PullRequestState State,
    RepositoryFullName RepositoryFullName,
    RepositoryDisplayName RepositoryDisplayName,
    RepositorySlug RepositorySlug,
    BranchName SourceBranch,
    BranchName DestinationBranch,
    Uri? HtmlUrl,
    CommitHash? MergeCommitHash,
    DateTimeOffset? UpdatedOn)
{
    /// <summary>
    /// Gets a value indicating whether the pull request is merged into the supplied target branch.
    /// </summary>
    /// <param name="targetBranch">The target branch.</param>
    /// <returns><see langword="true"/> when the pull request is merged into the target branch.</returns>
    public bool IsMergedInto(BranchName targetBranch)
    {
        return State.IsMerged &&
            string.Equals(DestinationBranch.Value, targetBranch.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a fallback Bitbucket pull request from Jira development metadata.
    /// </summary>
    /// <param name="candidate">The Jira pull request candidate.</param>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="repository">The repository identity.</param>
    /// <returns>The fallback normalized pull request.</returns>
    public static BitbucketPullRequest CreateMergedFallback(
        JiraPullRequestLink candidate,
        RepositoryFullName repositoryFullName,
        RepositoryRef repository)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new BitbucketPullRequest(
            candidate.Id,
            candidate.Status,
            repositoryFullName,
            new RepositoryDisplayName(repository.Slug.Value),
            repository.Slug,
            candidate.SourceBranch,
            candidate.DestinationBranch,
            candidate.Url,
            null,
            candidate.LastUpdatedOn);
    }
}
