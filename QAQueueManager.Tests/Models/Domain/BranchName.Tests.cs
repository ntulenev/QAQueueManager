using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class BranchNameTests
{
    [Fact(DisplayName = "Constructor throws when value is whitespace")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsWhitespaceThrowsArgumentException()
    {
        // Arrange
        var value = "   ";

        // Act
        Action act = () => _ = new BranchName(value);

        // Assert
        act.Should()
            .Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Constructor trims outer whitespace")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueHasOuterWhitespaceTrimsValue()
    {
        // Arrange
        var value = "  feature/qa-123  ";

        // Act
        var branchName = new BranchName(value);

        // Assert
        branchName.Value.Should().Be("feature/qa-123");
    }
}
