using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaQueueBuildProgressTests
{
    [Fact(DisplayName = "QaQueueBuildProgress exposes kind and issue key")]
    [Trait("Category", "Unit")]
    public void QaQueueBuildProgressExposesKindAndIssueKey()
    {
        // Act
        var progress = new QaQueueBuildProgress(QaQueueBuildProgressKind.CodeIssueCompleted, "Processed", 1, 2, "QA-1");

        // Assert
        progress.Kind.Should().Be(QaQueueBuildProgressKind.CodeIssueCompleted);
        progress.IssueKey.Should().Be("QA-1");
    }
}
