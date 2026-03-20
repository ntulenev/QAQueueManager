using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaQueueReportServiceVersionTokensTests
{
    [Fact(DisplayName = "QaQueueReportServiceVersionTokens expose stable labels")]
    [Trait("Category", "Unit")]
    public void QaQueueReportServiceVersionTokensExposeStableLabels()
    {
        // Assert
        QaQueueReportServiceVersionTokens.VERSION_NOT_FOUND.Should().Be("Version not found");
        QaQueueReportServiceVersionTokens.NO_TEAM.Should().Be("No team");
    }
}
