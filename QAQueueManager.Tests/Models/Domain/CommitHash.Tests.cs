using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class CommitHashTests
{
    [Fact(DisplayName = "CommitHash validates, matches prefixes, and exposes TryCreate")]
    [Trait("Category", "Unit")]
    public void CommitHashValidatesMatchesPrefixesAndExposesTryCreate()
    {
        // Arrange
        var fullHash = new CommitHash("abcdef1234");
        var shortHash = new CommitHash("abcdef1");

        // Act
        var created = CommitHash.TryCreate(" abcdef1 ", out var parsedHash);
        var invalidCreated = CommitHash.TryCreate("not-a-hash", out var invalidHash);

        // Assert
        created.Should().BeTrue();
        parsedHash.Should().Be(new CommitHash("abcdef1"));
        invalidCreated.Should().BeFalse();
        invalidHash.Should().Be(default(CommitHash));
        fullHash.Matches(shortHash).Should().BeTrue();
        shortHash.Matches(fullHash).Should().BeTrue();
        fullHash.ToString().Should().Be("abcdef1234");
    }

    [Fact(DisplayName = "CommitHash constructor throws for invalid values")]
    [Trait("Category", "Unit")]
    public void CommitHashConstructorWhenValueIsInvalidThrowsArgumentException()
    {
        // Arrange
        const string value = "xyz";

        // Act
        Action act = () => _ = new CommitHash(value);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
