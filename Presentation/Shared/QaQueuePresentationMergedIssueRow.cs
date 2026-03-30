namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Represents a repository row for an issue already merged into the target branch.
/// </summary>
/// <param name="Index">The one-based row index.</param>
/// <param name="Issue">The issue reference.</param>
/// <param name="Status">The formatted issue status.</param>
/// <param name="Assignee">The formatted assignee name.</param>
/// <param name="PullRequests">The formatted pull request summary.</param>
/// <param name="ArtifactVersion">The artifact version label.</param>
/// <param name="Alert">The formatted alert text.</param>
/// <param name="Source">The formatted source branch summary.</param>
/// <param name="Target">The formatted target branch summary.</param>
/// <param name="LastUpdated">The formatted last-updated value.</param>
/// <param name="Summary">The issue summary.</param>
internal sealed record QaQueuePresentationMergedIssueRow(
    int Index,
    QaQueuePresentationIssueRef Issue,
    string Status,
    string Assignee,
    string PullRequests,
    string ArtifactVersion,
    string Alert,
    string Source,
    string Target,
    string LastUpdated,
    string Summary);
