namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a Jira issue included in the QA queue.
/// </summary>
/// <param name="Id">The Jira issue identifier.</param>
/// <param name="Key">The Jira issue key.</param>
/// <param name="Summary">The issue summary.</param>
/// <param name="Status">The issue workflow status.</param>
/// <param name="DevelopmentSummary">The raw development field summary.</param>
/// <param name="Teams">The resolved team names.</param>
/// <param name="UpdatedAt">The last updated timestamp.</param>
internal sealed record QaIssue(
    long Id,
    string Key,
    string Summary,
    string Status,
    string DevelopmentSummary,
    IReadOnlyList<string> Teams,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>
    /// Gets a value indicating whether the issue is linked to code.
    /// </summary>
    public bool HasCode => !string.IsNullOrWhiteSpace(DevelopmentSummary) && DevelopmentSummary.Trim() != "{}";
}
