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
    JiraIssueId Id,
    JiraIssueKey Key,
    string Summary,
    JiraIssueStatus Status,
    string DevelopmentSummary,
    IReadOnlyList<TeamName> Teams,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>
    /// Gets a value indicating whether the issue is linked to code.
    /// </summary>
    public bool HasCode => !string.IsNullOrWhiteSpace(DevelopmentSummary) && DevelopmentSummary.Trim() != "{}";

    /// <summary>
    /// Returns the issue teams, falling back to the configured no-team token when none exist.
    /// </summary>
    /// <returns>The issue teams or the fallback team token.</returns>
    public IReadOnlyList<TeamName> GetTeamsOrFallback() => Teams.Count == 0 ? [TeamName.NoTeam] : Teams;
}
