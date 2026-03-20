namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a code-linked Jira issue that does not have a merge into the target branch.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositorySlug">The repository slug.</param>
/// <param name="PullRequests">The related pull requests.</param>
/// <param name="BranchNames">The related branch names.</param>
internal sealed record QaCodeIssueWithoutMerge(
    QaIssue Issue,
    string RepositoryFullName,
    RepositorySlug RepositorySlug,
    IReadOnlyList<JiraPullRequestLink> PullRequests,
    IReadOnlyList<string> BranchNames);
