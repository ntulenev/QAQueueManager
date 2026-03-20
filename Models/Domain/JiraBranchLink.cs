namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a Jira issue branch link.
/// </summary>
/// <param name="Name">The branch name.</param>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositoryUrl">The repository URL.</param>
internal sealed record JiraBranchLink(
    string Name,
    string RepositoryFullName,
    string RepositoryUrl);
