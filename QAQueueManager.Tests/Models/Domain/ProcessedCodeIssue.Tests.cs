using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class ProcessedCodeIssueTests
{
    [Fact(DisplayName = "ProcessedCodeIssue exposes provided resolutions")]
    [Trait("Category", "Unit")]
    public void ProcessedCodeIssueExposesProvidedResolutions()
    {
        // Arrange
        var issue = TestData.CreateIssue();
        var pullRequest = TestData.CreateJiraPullRequestLink(lastUpdatedOn: new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.Zero));
        var branch = TestData.CreateJiraBranchLink();
        var bitbucketPullRequest = TestData.CreateBitbucketPullRequest(updatedOn: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero));
        var withoutMerge = new IssueWithoutMergeData([pullRequest], [branch.Name]);
        var merged = new MergedIssueData(bitbucketPullRequest, new ArtifactVersion("1.2.3"));
        var resolution = new RepositoryResolution(
            new RepositoryRef(
                new RepositoryFullName("workspace/repo-a"),
                new RepositorySlug("repo-a")),
            withoutMerge,
            merged);

        // Act
        var processedIssue = new ProcessedCodeIssue(issue, [resolution]);

        // Assert
        processedIssue.Resolutions.Should().ContainSingle().Which.Should().Be(resolution);
    }
}
