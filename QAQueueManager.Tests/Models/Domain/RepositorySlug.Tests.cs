using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class RepositorySlugTests
{
    [Fact(DisplayName = "RepositorySlug validates values and resolves from repository names")]
    [Trait("Category", "Unit")]
    public void RepositorySlugValidatesValuesAndResolvesFromRepositoryNames()
    {
        // Arrange
        var slug = new RepositorySlug("repo-a");

        // Act
        var fromString = RepositorySlug.FromRepositoryFullName(" workspace\\repo-b ");
        var fromTyped = RepositorySlug.FromRepositoryFullName(new RepositoryFullName("workspace/repo-c"));
        var fromEmpty = RepositorySlug.FromRepositoryFullName(null);
        Action invalidAct = () => _ = new RepositorySlug("workspace/repo-a");

        // Assert
        slug.Value.Should().Be("repo-a");
        slug.ToString().Should().Be("repo-a");
        RepositorySlug.Unknown.Value.Should().Be("unknown");
        fromString.Should().Be(new RepositorySlug("repo-b"));
        fromTyped.Should().Be(new RepositorySlug("repo-c"));
        fromEmpty.Should().Be(RepositorySlug.Unknown);
        invalidAct.Should().Throw<ArgumentException>();
    }
}
