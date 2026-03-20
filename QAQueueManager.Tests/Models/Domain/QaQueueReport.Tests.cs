using FluentAssertions;

using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaQueueReportTests
{
    [Fact(DisplayName = "QaQueueReport indicates when it is grouped by team")]
    [Trait("Category", "Unit")]
    public void QaQueueReportIndicatesWhenItIsGroupedByTeam()
    {
        // Arrange
        var report = TestData.CreateReport(groupedByTeam: true);

        // Assert
        report.IsGroupedByTeam.Should().BeTrue();
    }
}
