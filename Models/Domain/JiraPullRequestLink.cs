namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a Jira issue pull request link.
/// </summary>
/// <param name="Id">The pull request identifier.</param>
/// <param name="Title">The pull request title.</param>
/// <param name="Status">The pull request status.</param>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositoryUrl">The repository URL.</param>
/// <param name="SourceBranch">The source branch name.</param>
/// <param name="DestinationBranch">The destination branch name.</param>
/// <param name="Url">The pull request URL.</param>
/// <param name="LastUpdatedOn">The last updated timestamp.</param>
internal sealed record JiraPullRequestLink(
    PullRequestId Id,
    string Title,
    PullRequestState Status,
    RepositoryFullName RepositoryFullName,
    Uri? RepositoryUrl,
    BranchName SourceBranch,
    BranchName DestinationBranch,
    Uri? Url,
    DateTimeOffset? LastUpdatedOn)
{
    /// <summary>
    /// Gets a value indicating whether the pull request is merged into the supplied target branch.
    /// </summary>
    /// <param name="targetBranch">The target branch.</param>
    /// <returns><see langword="true"/> when the pull request is merged into the target branch.</returns>
    public bool IsMergedInto(BranchName targetBranch)
    {
        return Status.IsMerged &&
            string.Equals(DestinationBranch.Value, targetBranch.Value, StringComparison.OrdinalIgnoreCase);
    }
}
