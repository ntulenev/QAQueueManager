using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class PendingMergedIssueTests
{
    [Fact(DisplayName = "PendingMergedIssue constructor stores merged pull request")]
    [Trait("Category", "Unit")]
    public void PendingMergedIssueConstructorStoresMergedPullRequest()
    {
        // Arrange
        var issue = TestData.CreateIssue();
        var mergedPullRequest = TestData.CreateMergedPullRequest(updatedOn: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero));

        // Act
        var pendingMergedIssue = new PendingMergedIssue(
            issue,
            new RepositoryRef(
                new RepositoryFullName("workspace/repo-a"),
                new RepositorySlug("repo-a")),
            mergedPullRequest);

        // Assert
        pendingMergedIssue.PullRequest.Should().Be(mergedPullRequest);
    }

    [Fact(DisplayName = "PendingMergedIssue factory method maps pull request data")]
    [Trait("Category", "Unit")]
    public void PendingMergedIssueFactoryMethodMapsPullRequestData()
    {
        // Arrange
        var issue = TestData.CreateIssue();
        var bitbucketPullRequest = TestData.CreateBitbucketPullRequest(
            id: 202,
            sourceBranch: "feature/qa-2",
            destinationBranch: "release/1.0",
            updatedOn: new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero));

        // Act
        var pendingMergedIssue = PendingMergedIssue.Create(
            issue,
            new RepositoryRef(
                new RepositoryFullName("workspace/repo-a"),
                new RepositorySlug("repo-a")),
            bitbucketPullRequest,
            new ArtifactVersion("2.0.0"));

        // Assert
        pendingMergedIssue.PullRequest.PullRequestId.Should().Be(new PullRequestId(202));
        pendingMergedIssue.PullRequest.SourceBranch.Should().Be(new BranchName("feature/qa-2"));
        pendingMergedIssue.PullRequest.DestinationBranch.Should().Be(new BranchName("release/1.0"));
        pendingMergedIssue.PullRequest.Version.Should().Be(new ArtifactVersion("2.0.0"));
    }
}
