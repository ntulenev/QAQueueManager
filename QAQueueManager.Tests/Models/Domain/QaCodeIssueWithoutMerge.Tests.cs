using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaCodeIssueWithoutMergeTests
{
    [Fact(DisplayName = "QaCodeIssueWithoutMerge exposes related pull requests")]
    [Trait("Category", "Unit")]
    public void QaCodeIssueWithoutMergeExposesRelatedPullRequests()
    {
        // Arrange
        var issue = TestData.CreateIssue();
        var pullRequest = TestData.CreateJiraPullRequestLink();
        var branch = TestData.CreateJiraBranchLink();

        // Act
        var noMergeIssue = new QaCodeIssueWithoutMerge(
            issue,
            new RepositoryFullName("workspace/repo-a"),
            new RepositorySlug("repo-a"),
            [pullRequest],
            [branch.Name]);

        // Assert
        noMergeIssue.PullRequests.Should().ContainSingle().Which.Should().Be(pullRequest);
    }
}
