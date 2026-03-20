using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class StringValueObjectsTests
{
    [Fact(DisplayName = "String-backed value objects trim and expose fallback tokens")]
    [Trait("Category", "Unit")]
    public void StringBackedValueObjectsTrimAndExposeFallbackTokens()
    {
        // Arrange
        var branch = new BranchName(" feature/qa-1 ");
        var issueKey = new JiraIssueKey(" QA-1 ");
        var issueStatus = new JiraIssueStatus(" In QA ");
        var pullRequestState = new PullRequestState(" merged ");
        var reportPath = new ReportFilePath(" reports\\qa-report.pdf ");
        var repositoryDisplayName = new RepositoryDisplayName(" Repo A ");
        var repositoryFullName = new RepositoryFullName(" workspace\\repo-a ");
        var teamName = new TeamName(" Core ");

        // Assert
        branch.Value.Should().Be("feature/qa-1");
        BranchName.Unknown.Value.Should().Be("-");
        issueKey.Value.Should().Be("QA-1");
        issueStatus.Value.Should().Be("In QA");
        JiraIssueStatus.Unknown.Value.Should().Be("-");
        pullRequestState.Value.Should().Be("merged");
        pullRequestState.IsMerged.Should().BeTrue();
        PullRequestState.Unknown.Value.Should().Be("UNKNOWN");
        PullRequestState.Merged.Value.Should().Be("MERGED");
        reportPath.Value.Should().Be("reports\\qa-report.pdf");
        repositoryDisplayName.Value.Should().Be("Repo A");
        repositoryFullName.Value.Should().Be("workspace/repo-a");
        RepositoryFullName.Unknown.Value.Should().Be("Unknown repository");
        teamName.Value.Should().Be("Core");
        TeamName.NoTeam.Value.Should().Be(QaQueueReportServiceVersionTokens.NO_TEAM);
    }
}
