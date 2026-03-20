namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents the full QA queue report.
/// </summary>
/// <param name="GeneratedAt">The report generation timestamp.</param>
/// <param name="Title">The report title.</param>
/// <param name="Jql">The Jira query used to produce the report.</param>
/// <param name="TargetBranch">The configured target branch.</param>
/// <param name="TeamGroupingField">The Jira team field used for grouping, if any.</param>
/// <param name="HideNoCodeIssues">Whether no-code issues are hidden in the report.</param>
/// <param name="NoCodeIssues">The issues without code links.</param>
/// <param name="Repositories">The repository sections when team grouping is disabled.</param>
/// <param name="Teams">The team sections when team grouping is enabled.</param>
internal sealed record QaQueueReport(
    DateTimeOffset GeneratedAt,
    string Title,
    string Jql,
    string TargetBranch,
    string? TeamGroupingField,
    bool HideNoCodeIssues,
    IReadOnlyList<QaIssue> NoCodeIssues,
    IReadOnlyList<QaRepositorySection> Repositories,
    IReadOnlyList<QaTeamSection> Teams)
{
    /// <summary>
    /// Gets a value indicating whether the report is grouped by team.
    /// </summary>
    public bool IsGroupedByTeam => !string.IsNullOrWhiteSpace(TeamGroupingField);
}
