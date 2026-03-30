using FluentAssertions;

using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class PullRequestDomainMethodsTests
{
    [Fact(DisplayName = "JiraPullRequestLink IsMergedInto matches merged target branch")]
    [Trait("Category", "Unit")]
    public void JiraPullRequestLinkIsMergedIntoMatchesMergedTargetBranch()
    {
        // Arrange
        var pullRequest = TestData.CreateJiraPullRequestLink(status: "MERGED", destinationBranch: "main");

        // Act
        var isMergedIntoTarget = pullRequest.IsMergedInto(new QAQueueManager.Models.Domain.BranchName("main"));
        var isMergedIntoOther = pullRequest.IsMergedInto(new QAQueueManager.Models.Domain.BranchName("release/1.0"));

        // Assert
        isMergedIntoTarget.Should().BeTrue();
        isMergedIntoOther.Should().BeFalse();
    }

    [Fact(DisplayName = "BitbucketPullRequest IsMergedInto matches merged target branch")]
    [Trait("Category", "Unit")]
    public void BitbucketPullRequestIsMergedIntoMatchesMergedTargetBranch()
    {
        // Arrange
        var pullRequest = TestData.CreateBitbucketPullRequest(state: "MERGED", destinationBranch: "main");

        // Act
        var isMergedIntoTarget = pullRequest.IsMergedInto(new QAQueueManager.Models.Domain.BranchName("main"));
        var isMergedIntoOther = pullRequest.IsMergedInto(new QAQueueManager.Models.Domain.BranchName("release/1.0"));

        // Assert
        isMergedIntoTarget.Should().BeTrue();
        isMergedIntoOther.Should().BeFalse();
    }
}
