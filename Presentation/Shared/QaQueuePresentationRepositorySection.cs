namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Represents one repository section in the presentation document.
/// </summary>
/// <param name="RepositoryName">The repository display name.</param>
/// <param name="WithoutTargetMerge">The rows for issues without a target-branch merge.</param>
/// <param name="MergedIssueRows">The rows for issues already merged into the target branch.</param>
internal sealed record QaQueuePresentationRepositorySection(
    string RepositoryName,
    IReadOnlyList<QaQueuePresentationWithoutMergeRow> WithoutTargetMerge,
    IReadOnlyList<QaQueuePresentationMergedIssueRow> MergedIssueRows);
