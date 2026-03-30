using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class RepositoryAccumulatorTests
{
    [Fact(DisplayName = "RepositoryAccumulator groups merged rows and sorts no-merge issues")]
    [Trait("Category", "Unit")]
    public void RepositoryAccumulatorGroupsMergedRowsAndSortsNoMergeIssues()
    {
        // Arrange
        var repositoryFullName = new RepositoryFullName("workspace/repo-a");
        var repositorySlug = new RepositorySlug("repo-a");
        var accumulator = new RepositoryAccumulator(repositoryFullName, repositorySlug);
        var laterIssue = TestData.CreateIssue(id: 1002, key: "QA-20", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var earlierIssue = TestData.CreateIssue(id: 1001, key: "QA-10", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var sharedIssue = TestData.CreateIssue(id: 1003, key: "QA-30", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var pullRequestA = TestData.CreateBitbucketPullRequest(id: 301, updatedOn: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
        var pullRequestB = TestData.CreateBitbucketPullRequest(id: 302, updatedOn: new DateTimeOffset(2026, 3, 20, 13, 0, 0, TimeSpan.Zero));

        // Act
        accumulator.AddWithoutMerge(laterIssue, [TestData.CreateJiraPullRequestLink(id: 401)], [new BranchName("feature/qa-20")]);
        accumulator.AddWithoutMerge(earlierIssue, [TestData.CreateJiraPullRequestLink(id: 402)], [new BranchName("feature/qa-10")]);
        accumulator.AddMerged(sharedIssue, pullRequestA, new ArtifactVersion("1.0.0"));
        accumulator.AddMerged(sharedIssue, pullRequestB, new ArtifactVersion("2.0.0"));

        var built = accumulator.Build();

        // Assert
        built.RepositoryFullName.Should().Be(repositoryFullName);
        built.RepositorySlug.Should().Be(repositorySlug);
        built.WithoutTargetMerge.Select(static item => item.Issue.Key.Value).Should().ContainInOrder("QA-10", "QA-20");
        built.MergedIssueRows.Should().HaveCount(2);
        built.MergedIssueRows.All(static row => !row.HasDuplicateIssue).Should().BeTrue();
        built.MergedIssueRows.Select(static row => row.Version.Value).Should().ContainInOrder("1.0.0", "2.0.0");
        built.MergedIssueRows[1].PullRequests.Should().ContainSingle().Which.PullRequestId.Should().Be(new PullRequestId(302));
    }

    [Fact(DisplayName = "RepositoryAccumulator GetOrAdd reuses existing accumulator for matching repository")]
    [Trait("Category", "Unit")]
    public void RepositoryAccumulatorGetOrAddWhenRepositoryExistsReusesAccumulator()
    {
        // Arrange
        var repositories = new Dictionary<string, RepositoryAccumulator>(StringComparer.OrdinalIgnoreCase);
        var repositoryFullName = new RepositoryFullName("workspace/repo-a");
        var repositorySlug = new RepositorySlug("repo-a");

        // Act
        var created = RepositoryAccumulator.GetOrAdd(repositories, repositoryFullName, repositorySlug);
        var reused = RepositoryAccumulator.GetOrAdd(repositories, repositoryFullName, repositorySlug);

        // Assert
        repositories.Should().ContainSingle();
        created.Should().BeSameAs(reused);
    }
}
