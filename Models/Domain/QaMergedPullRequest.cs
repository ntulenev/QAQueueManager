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
    string SourceBranch,
    string DestinationBranch,
    string Version,
    string PullRequestUrl,
    CommitHash? MergeCommitHash,
    DateTimeOffset? PullRequestUpdatedOn);
