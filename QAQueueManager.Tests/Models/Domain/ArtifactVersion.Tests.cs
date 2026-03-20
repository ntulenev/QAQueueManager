using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class ArtifactVersionTests
{
    [Fact(DisplayName = "ArtifactVersion trims values and recognizes not-found token")]
    [Trait("Category", "Unit")]
    public void ArtifactVersionTrimsValuesAndRecognizesNotFoundToken()
    {
        // Arrange
        var version = new ArtifactVersion(" 1.2.3 ");

        // Assert
        version.Value.Should().Be("1.2.3");
        version.ToString().Should().Be("1.2.3");
        ArtifactVersion.NotFound.IsNotFound.Should().BeTrue();
    }
}
