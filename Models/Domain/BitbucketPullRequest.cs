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
    int Id,
    string State,
    string RepositoryFullName,
    string RepositoryDisplayName,
    string RepositorySlug,
    string SourceBranch,
    string DestinationBranch,
    string HtmlUrl,
    string MergeCommitHash,
    DateTimeOffset? UpdatedOn);
