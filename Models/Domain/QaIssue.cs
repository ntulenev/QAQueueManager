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

    /// <summary>
    /// Returns normalized team names for the issue.
    /// </summary>
    /// <returns>The trimmed, distinct team names.</returns>
    public IReadOnlyList<string> GetNormalizedTeams() =>
    [
        .. Teams
            .Where(static team => !string.IsNullOrWhiteSpace(team))
            .Select(static team => team.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    /// <summary>
    /// Returns normalized team names, falling back to the configured no-team token when none exist.
    /// </summary>
    /// <returns>The normalized team names or the fallback team token.</returns>
    public List<string> GetTeamsOrFallback()
    {
        var teams = GetNormalizedTeams();
        return teams.Count == 0 ? [QaQueueReportServiceVersionTokens.NO_TEAM] : [.. teams];
    }
}
