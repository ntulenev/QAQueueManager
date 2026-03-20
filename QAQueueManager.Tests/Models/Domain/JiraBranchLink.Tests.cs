using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class JiraBranchLinkTests
{
    [Fact(DisplayName = "JiraBranchLink exposes constructed repository metadata")]
    [Trait("Category", "Unit")]
    public void JiraBranchLinkExposesConstructedRepositoryMetadata()
    {
        // Arrange
        var branch = TestData.CreateJiraBranchLink();

        // Assert
        branch.RepositoryFullName.Should().Be(new RepositoryFullName("workspace/repo-a"));
    }
}
