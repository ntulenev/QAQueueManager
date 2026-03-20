using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaMergedPullRequestTests
{
    [Fact(DisplayName = "QaMergedPullRequest factory method maps pull request data")]
    [Trait("Category", "Unit")]
    public void QaMergedPullRequestFactoryMethodMapsPullRequestData()
    {
        // Arrange
        var bitbucketPullRequest = TestData.CreateBitbucketPullRequest(
            id: 202,
            sourceBranch: "feature/qa-2",
            destinationBranch: "release/1.0",
            updatedOn: new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero));

        // Act
        var mergedPullRequest = QaMergedPullRequest.FromBitbucketPullRequest(bitbucketPullRequest, new ArtifactVersion("2.0.0"));

        // Assert
        mergedPullRequest.PullRequestId.Should().Be(new PullRequestId(202));
        mergedPullRequest.SourceBranch.Should().Be(new BranchName("feature/qa-2"));
        mergedPullRequest.DestinationBranch.Should().Be(new BranchName("release/1.0"));
        mergedPullRequest.Version.Should().Be(new ArtifactVersion("2.0.0"));
    }
}
