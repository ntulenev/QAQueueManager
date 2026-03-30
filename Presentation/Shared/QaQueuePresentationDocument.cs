namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Presentation-neutral document view model for QA queue rendering.
/// </summary>
/// <param name="Header">The document header metadata.</param>
/// <param name="HideNoCodeIssues">Whether no-code issues are hidden in the rendered output.</param>
/// <param name="NoCodeIssues">The top-level no-code issue rows.</param>
/// <param name="Repositories">The repository sections when grouping is disabled.</param>
/// <param name="Teams">The team sections when grouping is enabled.</param>
internal sealed record QaQueuePresentationDocument(
    QaQueuePresentationDocumentHeader Header,
    bool HideNoCodeIssues,
    IReadOnlyList<QaQueuePresentationNoCodeIssueRow> NoCodeIssues,
    IReadOnlyList<QaQueuePresentationRepositorySection> Repositories,
    IReadOnlyList<QaQueuePresentationTeamSection> Teams)
{
    /// <summary>
    /// Gets a value indicating whether the document is grouped by team.
    /// </summary>
    public bool IsGroupedByTeam => !string.IsNullOrWhiteSpace(Header.TeamGroupingField);
}
