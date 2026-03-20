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
    string Status,
    string RepositoryFullName,
    string RepositoryUrl,
    string SourceBranch,
    string DestinationBranch,
    string Url,
    DateTimeOffset? LastUpdatedOn);
