namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents one merged report row for a concrete issue and artifact version.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositorySlug">The repository slug.</param>
/// <param name="Version">The artifact version.</param>
/// <param name="PullRequests">The pull requests associated with this version.</param>
/// <param name="HasDuplicateIssue">Whether the issue appears multiple times in the report.</param>
internal sealed record QaMergedIssueVersionRow(
    QaIssue Issue,
    RepositoryFullName RepositoryFullName,
    RepositorySlug RepositorySlug,
    ArtifactVersion Version,
    IReadOnlyList<QaMergedPullRequest> PullRequests,
    bool HasDuplicateIssue);
