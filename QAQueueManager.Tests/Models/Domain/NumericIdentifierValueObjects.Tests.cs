using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class NumericIdentifierValueObjectsTests
{
    [Fact(DisplayName = "Numeric identifier value objects validate positive values and format invariant strings")]
    [Trait("Category", "Unit")]
    public void NumericIdentifierValueObjectsValidatePositiveValuesAndFormatInvariantStrings()
    {
        // Arrange
        var issueId = new JiraIssueId(42);
        var pullRequestId = new PullRequestId(7);

        // Act
        Action invalidIssueId = () => _ = new JiraIssueId(0);
        Action invalidPullRequestId = () => _ = new PullRequestId(-1);

        // Assert
        issueId.Value.Should().Be(42);
        issueId.ToString().Should().Be("42");
        pullRequestId.Value.Should().Be(7);
        pullRequestId.ToString().Should().Be("7");
        invalidIssueId.Should().Throw<ArgumentOutOfRangeException>();
        invalidPullRequestId.Should().Throw<ArgumentOutOfRangeException>();
    }
}
