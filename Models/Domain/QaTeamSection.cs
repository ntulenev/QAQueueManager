namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents one team section in the report.
/// </summary>
/// <param name="Team">The team name.</param>
/// <param name="NoCodeIssues">The team's issues without code links.</param>
/// <param name="Repositories">The team's repository sections.</param>
internal sealed record QaTeamSection(
    string Team,
    IReadOnlyList<QaIssue> NoCodeIssues,
    IReadOnlyList<QaRepositorySection> Repositories);
