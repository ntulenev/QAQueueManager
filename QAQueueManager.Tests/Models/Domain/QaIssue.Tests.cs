using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaIssueTests
{
    [Fact(DisplayName = "GetTeamsOrFallback returns configured teams when present")]
    [Trait("Category", "Unit")]
    public void GetTeamsOrFallbackWhenTeamsExistReturnsTeams()
    {
        // Arrange
        var developmentSummary = /*lang=json,strict*/ """{}""";
        var issue = new QaIssue(
            new JiraIssueId(1001),
            new JiraIssueKey("QA-1"),
            "Summary",
            new JiraIssueStatus("Open"),
            "QA Engineer",
            developmentSummary,
            [new TeamName("Core"), new TeamName("Platform")],
            null);

        // Act
        var teams = issue.GetTeamsOrFallback();

        // Assert
        teams.Should().ContainInOrder(new TeamName("Core"), new TeamName("Platform"));
    }

    [Fact(DisplayName = "GetTeamsOrFallback returns no-team token when teams are empty")]
    [Trait("Category", "Unit")]
    public void GetTeamsOrFallbackWhenTeamsAreEmptyReturnsFallback()
    {
        // Arrange
        var developmentSummary = /*lang=json,strict*/ """{}""";
        var issue = new QaIssue(
            new JiraIssueId(1002),
            new JiraIssueKey("QA-2"),
            "Summary",
            new JiraIssueStatus("Open"),
            "QA Engineer",
            developmentSummary,
            [],
            null);

        // Act
        var teams = issue.GetTeamsOrFallback();

        // Assert
        teams.Should().ContainSingle()
            .Which.Should().Be(TeamName.NoTeam);
    }
}
