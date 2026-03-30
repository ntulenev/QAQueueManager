using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaIssueTests
{
    [Fact(DisplayName = "Create normalizes defaults and de-duplicates teams")]
    [Trait("Category", "Unit")]
    public void CreateNormalizesDefaultsAndDeduplicatesTeams()
    {
        // Arrange
        var teams = new[]
        {
            new TeamName("Core"),
            new TeamName(" core "),
            new TeamName("Platform")
        };

        // Act
        var issue = QaIssue.Create(
            new JiraIssueId(1000),
            new JiraIssueKey("QA-0"),
            "  ",
            null,
            "",
            null,
            teams,
            null);

        // Assert
        issue.Summary.Should().Be("-");
        issue.Status.Should().Be(JiraIssueStatus.Unknown);
        issue.Assignee.Should().Be("-");
        issue.DevelopmentSummary.Should().Be("{}");
        issue.Teams.Should().ContainInOrder(new TeamName("Core"), new TeamName("Platform"));
    }

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

    [Fact(DisplayName = "HasCode returns false when development summary explicitly reports no development")]
    [Trait("Category", "Unit")]
    public void HasCodeWhenDevelopmentSummaryReportsZeroPullRequestsAndBranchesReturnsFalse()
    {
        // Arrange
        var developmentSummary = /*lang=json,strict*/ """{"pullRequests":0,"branches":0}""";
        var issue = new QaIssue(
            new JiraIssueId(1003),
            new JiraIssueKey("QA-3"),
            "Summary",
            new JiraIssueStatus("Open"),
            "QA Engineer",
            developmentSummary,
            [],
            null);

        // Act
        var hasCode = issue.HasCode;

        // Assert
        hasCode.Should().BeFalse();
        issue.DevelopmentState.HasKnownNoDevelopment.Should().BeTrue();
    }

    [Fact(DisplayName = "HasCode returns true for non-empty unparseable development summary")]
    [Trait("Category", "Unit")]
    public void HasCodeWhenDevelopmentSummaryIsNonEmptyButUnknownReturnsTrue()
    {
        // Arrange
        var issue = new QaIssue(
            new JiraIssueId(1004),
            new JiraIssueKey("QA-4"),
            "Summary",
            new JiraIssueStatus("Open"),
            "QA Engineer",
            "Development",
            [],
            null);

        // Act
        var hasCode = issue.HasCode;

        // Assert
        hasCode.Should().BeTrue();
        issue.DevelopmentState.HasKnownNoDevelopment.Should().BeFalse();
    }
}
