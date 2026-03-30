namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Represents a no-code issue row in the presentation document.
/// </summary>
/// <param name="Index">The one-based row index.</param>
/// <param name="Issue">The issue reference.</param>
/// <param name="Status">The formatted issue status.</param>
/// <param name="Assignee">The formatted assignee name.</param>
/// <param name="LastUpdated">The formatted last-updated value.</param>
/// <param name="Summary">The issue summary.</param>
internal sealed record QaQueuePresentationNoCodeIssueRow(
    int Index,
    QaQueuePresentationIssueRef Issue,
    string Status,
    string Assignee,
    string LastUpdated,
    string Summary);
