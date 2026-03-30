namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Stores top-level report metadata shared across renderers.
/// </summary>
/// <param name="Title">The document title.</param>
/// <param name="GeneratedAt">The formatted report generation timestamp.</param>
/// <param name="TargetBranch">The target branch label.</param>
/// <param name="Jql">The Jira query text.</param>
/// <param name="TeamGroupingField">The team grouping field when grouping is enabled.</param>
/// <param name="RepositoryCount">The total repository count displayed in the header.</param>
/// <param name="NoCodeIssueCount">The total no-code issue count displayed in the header.</param>
/// <param name="TeamCount">The total team count displayed in the header.</param>
internal sealed record QaQueuePresentationDocumentHeader(
    string Title,
    string GeneratedAt,
    string TargetBranch,
    string Jql,
    string? TeamGroupingField,
    int RepositoryCount,
    int NoCodeIssueCount,
    int TeamCount);
