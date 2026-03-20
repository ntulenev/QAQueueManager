namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents progress information emitted while building the QA report.
/// </summary>
/// <param name="Kind">The type of progress event.</param>
/// <param name="Message">The human-readable progress message.</param>
/// <param name="Current">The current progress value.</param>
/// <param name="Total">The total progress value.</param>
/// <param name="IssueKey">The related issue key when applicable.</param>
internal sealed record QaQueueBuildProgress(
    QaQueueBuildProgressKind Kind,
    string? Message = null,
    int Current = 0,
    int Total = 0,
    string? IssueKey = null);
