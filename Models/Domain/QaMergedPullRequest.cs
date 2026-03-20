namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a merged pull request associated with a report row.
/// </summary>
/// <param name="PullRequestId">The pull request identifier.</param>
/// <param name="SourceBranch">The source branch name.</param>
/// <param name="DestinationBranch">The destination branch name.</param>
/// <param name="Version">The resolved artifact version.</param>
/// <param name="PullRequestUrl">The pull request URL.</param>
/// <param name="MergeCommitHash">The merge commit hash.</param>
/// <param name="PullRequestUpdatedOn">The last updated timestamp.</param>
internal sealed record QaMergedPullRequest(
    PullRequestId PullRequestId,
    BranchName SourceBranch,
    BranchName DestinationBranch,
    ArtifactVersion Version,
    Uri? PullRequestUrl,
    CommitHash? MergeCommitHash,
    DateTimeOffset? PullRequestUpdatedOn)
{
    /// <summary>
    /// Creates a merged pull request from a normalized Bitbucket pull request.
    /// </summary>
    /// <param name="pullRequest">The normalized Bitbucket pull request.</param>
    /// <param name="version">The resolved artifact version.</param>
    /// <returns>The mapped merged pull request.</returns>
    public static QaMergedPullRequest FromBitbucketPullRequest(
        BitbucketPullRequest pullRequest,
        ArtifactVersion version)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        return new QaMergedPullRequest(
            pullRequest.Id,
            pullRequest.SourceBranch,
            pullRequest.DestinationBranch,
            version,
            pullRequest.HtmlUrl,
            pullRequest.MergeCommitHash,
            pullRequest.UpdatedOn);
    }
}
