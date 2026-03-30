namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a Jira issue included in the QA queue.
/// </summary>
/// <param name="Id">The Jira issue identifier.</param>
/// <param name="Key">The Jira issue key.</param>
/// <param name="Summary">The issue summary.</param>
/// <param name="Status">The issue workflow status.</param>
/// <param name="Assignee">The issue assignee display name.</param>
/// <param name="DevelopmentSummary">The raw development field summary.</param>
/// <param name="Teams">The resolved team names.</param>
/// <param name="UpdatedAt">The last updated timestamp.</param>
internal sealed record QaIssue(
    JiraIssueId Id,
    JiraIssueKey Key,
    string Summary,
    JiraIssueStatus Status,
    string Assignee,
    string DevelopmentSummary,
    IReadOnlyList<TeamName> Teams,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>
    /// Creates a normalized QA issue from raw field values.
    /// </summary>
    /// <param name="id">The Jira issue identifier.</param>
    /// <param name="key">The Jira issue key.</param>
    /// <param name="summary">The raw issue summary.</param>
    /// <param name="status">The raw issue status.</param>
    /// <param name="assignee">The raw issue assignee.</param>
    /// <param name="developmentSummary">The raw development field summary.</param>
    /// <param name="teams">The raw resolved team names.</param>
    /// <param name="updatedAt">The last updated timestamp.</param>
    /// <returns>The normalized issue.</returns>
    public static QaIssue Create(
        JiraIssueId id,
        JiraIssueKey key,
        string? summary,
        string? status,
        string? assignee,
        string? developmentSummary,
        IReadOnlyList<TeamName>? teams,
        DateTimeOffset? updatedAt)
    {
        IReadOnlyList<TeamName> normalizedTeams = teams is null
            ? []
            : [.. teams
                .GroupBy(static team => team.Value, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())];

        return new QaIssue(
            id,
            key,
            NormalizeOrDefault(summary, MISSING_DISPLAY_VALUE),
            new JiraIssueStatus(NormalizeOrDefault(status, JiraIssueStatus.Unknown.Value)),
            NormalizeOrDefault(assignee, MISSING_DISPLAY_VALUE),
            NormalizeOrDefault(developmentSummary, EMPTY_DEVELOPMENT_SUMMARY),
            normalizedTeams,
            updatedAt);
    }

    /// <summary>
    /// Gets the normalized development state derived from the raw Jira field payload.
    /// </summary>
    public QaIssueDevelopmentState DevelopmentState => QaIssueDevelopmentState.Parse(DevelopmentSummary);

    /// <summary>
    /// Gets a value indicating whether the issue is linked to code.
    /// </summary>
    public bool HasCode => DevelopmentState.HasCode;

    /// <summary>
    /// Returns the issue teams, falling back to the configured no-team token when none exist.
    /// </summary>
    /// <returns>The issue teams or the fallback team token.</returns>
    public IReadOnlyList<TeamName> GetTeamsOrFallback() => Teams.Count == 0 ? [TeamName.NoTeam] : Teams;

    private static string NormalizeOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private const string MISSING_DISPLAY_VALUE = "-";
    private const string EMPTY_DEVELOPMENT_SUMMARY = "{}";
}
