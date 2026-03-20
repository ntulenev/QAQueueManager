using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class RepositoryFullNameTests
{
    [Fact(DisplayName = "Constructor throws when value is empty")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueIsEmptyThrowsArgumentException()
    {
        // Arrange
        var value = string.Empty;

        // Act
        Action act = () => _ = new RepositoryFullName(value);

        // Assert
        act.Should()
            .Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Constructor normalizes slashes and trims")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenValueContainsBackslashesNormalizesSeparators()
    {
        // Arrange
        var value = "  workspace\\repo-a  ";

        // Act
        var repositoryFullName = new RepositoryFullName(value);

        // Assert
        repositoryFullName.Value.Should().Be("workspace/repo-a");
    }
}
