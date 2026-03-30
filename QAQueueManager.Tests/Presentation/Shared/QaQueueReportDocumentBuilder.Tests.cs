using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;
using QAQueueManager.Presentation.Shared;
using QAQueueManager.Tests.Testing;

#pragma warning disable CA1716
namespace QAQueueManager.Tests.Presentation.Shared;
#pragma warning restore CA1716

public sealed class QaQueueReportDocumentBuilderTests
{
    [Fact(DisplayName = "Build maps the QA report into a shared presentation document")]
    [Trait("Category", "Unit")]
    public void BuildMapsQaReportIntoSharedPresentationDocument()
    {
        var builder = new QaQueueReportDocumentBuilder(Options.Create(new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/", UriKind.Absolute)
        }));
        var report = TestData.CreateReport(groupedByTeam: true);

        var document = builder.Build(report);

        document.IsGroupedByTeam.Should().BeTrue();
        document.Header.Title.Should().Be("QA Queue");
        document.Header.TargetBranch.Should().Be("main");
        document.Header.RepositoryCount.Should().Be(1);
        document.Header.TeamCount.Should().Be(1);
        document.Header.NoCodeIssueCount.Should().Be(1);
        document.NoCodeIssues.Should().ContainSingle();
        document.NoCodeIssues[0].Issue.Url.Should().Be("https://jira.example.test/browse/QA-1");
        document.Teams.Should().ContainSingle(team => team.TeamName == "Core");
        document.Teams[0].Repositories.Should().ContainSingle();
        document.Teams[0].Repositories[0].MergedIssueRows.Should().ContainSingle(row => row.Issue.Highlight);
    }
}
