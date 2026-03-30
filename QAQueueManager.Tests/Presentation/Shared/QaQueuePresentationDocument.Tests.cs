using FluentAssertions;

using QAQueueManager.Presentation.Shared;

#pragma warning disable CA1716
namespace QAQueueManager.Tests.Presentation.Shared;
#pragma warning restore CA1716

public sealed class QaQueuePresentationDocumentTests
{
    [Fact(DisplayName = "IsGroupedByTeam reflects the header grouping field")]
    [Trait("Category", "Unit")]
    public void IsGroupedByTeamReflectsHeaderGroupingField()
    {
        var grouped = new QaQueuePresentationDocument(
            new QaQueuePresentationDocumentHeader("Title", "Generated", "main", "project = QA", "Team", 1, 1, 1),
            HideNoCodeIssues: false,
            NoCodeIssues: [],
            Repositories: [],
            Teams: []);
        var notGrouped = new QaQueuePresentationDocument(
            new QaQueuePresentationDocumentHeader("Title", "Generated", "main", "project = QA", null, 1, 1, 0),
            HideNoCodeIssues: false,
            NoCodeIssues: [],
            Repositories: [],
            Teams: []);

        grouped.IsGroupedByTeam.Should().BeTrue();
        notGrouped.IsGroupedByTeam.Should().BeFalse();
    }
}
