using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class BitbucketTagTests
{
    [Fact(DisplayName = "BitbucketTag exposes constructed values")]
    [Trait("Category", "Unit")]
    public void BitbucketTagExposesConstructedValues()
    {
        // Arrange
        var issue = TestData.CreateIssue(1001, "QA-1", updatedAt: new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.Zero));
        var tag = new BitbucketTag(new ArtifactVersion("1.2.3"), new CommitHash("abcdef1"), issue.UpdatedAt);

        // Assert
        tag.Name.Should().Be(new ArtifactVersion("1.2.3"));
        tag.TargetHash.Should().Be(new CommitHash("abcdef1"));
        tag.TaggedOn.Should().Be(issue.UpdatedAt);
    }
}
