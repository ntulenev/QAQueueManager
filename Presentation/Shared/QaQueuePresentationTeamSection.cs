namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Represents a team section in the presentation document.
/// </summary>
/// <param name="TeamName">The team name.</param>
/// <param name="NoCodeIssues">The no-code issues assigned to the team.</param>
/// <param name="Repositories">The repository sections assigned to the team.</param>
internal sealed record QaQueuePresentationTeamSection(
    string TeamName,
    IReadOnlyList<QaQueuePresentationNoCodeIssueRow> NoCodeIssues,
    IReadOnlyList<QaQueuePresentationRepositorySection> Repositories);
