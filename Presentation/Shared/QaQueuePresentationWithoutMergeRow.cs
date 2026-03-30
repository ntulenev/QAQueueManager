namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Represents a repository row for an issue without a merge into the target branch.
/// </summary>
/// <param name="Index">The one-based row index.</param>
/// <param name="Issue">The issue reference.</param>
/// <param name="Status">The formatted issue status.</param>
/// <param name="Assignee">The formatted assignee name.</param>
/// <param name="PullRequests">The formatted pull request summary.</param>
/// <param name="Branches">The formatted branch summary.</param>
/// <param name="Alert">The formatted alert text.</param>
/// <param name="LastUpdated">The formatted last-updated value.</param>
/// <param name="Summary">The issue summary.</param>
internal sealed record QaQueuePresentationWithoutMergeRow(
    int Index,
    QaQueuePresentationIssueRef Issue,
    string Status,
    string Assignee,
    string PullRequests,
    string Branches,
    string Alert,
    string LastUpdated,
    string Summary);
