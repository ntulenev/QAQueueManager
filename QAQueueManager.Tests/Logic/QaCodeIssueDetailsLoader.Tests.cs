using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using QAQueueManager.Abstractions;
using QAQueueManager.Logic;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Logic;

public sealed class QaCodeIssueDetailsLoaderTests
{
    [Fact(DisplayName = "LoadAsync skips branch lookup when development summary reports zero branches")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenDevelopmentSummaryReportsZeroBranchesSkipsBranchLookup()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(101, "QA-101", developmentSummary: /*lang=json,strict*/ """{"branches":0}""");
        var progressReports = new List<QaQueueBuildProgress>();
        var progress = new Progress<QaQueueBuildProgress>(progressReports.Add);
        var pullRequestCalls = 0;
        var branchCalls = 0;

        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(
                It.Is<JiraIssueId>(id => id == issue.Id),
                It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)))
            .Callback(() => pullRequestCalls++)
            .ReturnsAsync([]);
        jiraDevelopmentClient
            .Setup(client => client.GetBranchesAsync(
                It.Is<JiraIssueId>(id => id == issue.Id),
                It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)))
            .Callback(() => branchCalls++)
            .ReturnsAsync([]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress, cts.Token);

        // Assert
        pullRequestCalls.Should().Be(1);
        branchCalls.Should().Be(0);
        result.Should().ContainSingle();
        result[0].Resolutions.Should().ContainSingle();
        result[0].Resolutions[0].RepositoryFullName.Should().Be(RepositoryFullName.Unknown);
        result[0].Resolutions[0].RepositorySlug.Should().Be(RepositorySlug.Unknown);
        result[0].Resolutions[0].WithoutMerge.Should().NotBeNull();
        progressReports.Should().Contain(report => report.Kind == QaQueueBuildProgressKind.CodeAnalysisStarted && report.Total == 1);
        progressReports.Should().Contain(report => report.Kind == QaQueueBuildProgressKind.CodeIssueStarted && report.IssueKey == "QA-101");
        progressReports.Should().Contain(report => report.Kind == QaQueueBuildProgressKind.CodeAnalysisCompleted && report.Total == 1);
    }

    [Fact(DisplayName = "LoadAsync skips all Jira dev-status lookups when development summary reports zero pull requests and branches")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenDevelopmentSummaryReportsNoDevelopmentSkipsAllDevStatusLookups()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(
            107,
            "QA-107",
            developmentSummary: /*lang=json,strict*/ """{"pullRequests":0,"branches":0}""");

        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        result.Should().ContainSingle();
        var resolution = result[0].Resolutions.Should().ContainSingle().Subject;
        resolution.RepositoryFullName.Should().Be(RepositoryFullName.Unknown);
        resolution.RepositorySlug.Should().Be(RepositorySlug.Unknown);
        resolution.WithoutMerge.Should().NotBeNull();
        resolution.WithoutMerge!.PullRequests.Should().BeEmpty();
        resolution.WithoutMerge.BranchNames.Should().BeEmpty();
    }

    [Fact(DisplayName = "LoadAsync resolves merged pull requests and artifact versions")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenIssueHasMergedPullRequestResolvesArtifactVersion()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(102, "QA-102", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var jiraPullRequest = TestData.CreateJiraPullRequestLink(
            id: 42,
            status: "MERGED",
            destinationBranch: "main",
            lastUpdatedOn: new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.Zero));
        var bitbucketPullRequest = TestData.CreateBitbucketPullRequest(
            id: 42,
            state: "MERGED",
            mergeCommitHash: "abcdef1",
            updatedOn: new DateTimeOffset(2026, 3, 20, 9, 30, 0, TimeSpan.Zero));
        var pullRequestCalls = 0;
        var bitbucketCalls = 0;
        var tagCalls = 0;

        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(
                It.Is<JiraIssueId>(id => id == issue.Id),
                It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)))
            .Callback(() => pullRequestCalls++)
            .ReturnsAsync([jiraPullRequest]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        bitbucketClient
            .Setup(client => client.GetPullRequestAsync(
                It.Is<RepositorySlug>(slug => slug == new RepositorySlug("repo-a")),
                It.Is<PullRequestId>(id => id == new PullRequestId(42)),
                It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)))
            .Callback(() => bitbucketCalls++)
            .ReturnsAsync(bitbucketPullRequest);
        bitbucketClient
            .Setup(client => client.GetTagsByCommitHashAsync(
                It.Is<RepositorySlug>(slug => slug == new RepositorySlug("repo-a")),
                It.Is<CommitHash>(hash => hash == new CommitHash("abcdef1")),
                It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)))
            .Callback(() => tagCalls++)
            .ReturnsAsync([new BitbucketTag(new ArtifactVersion("2.0.0"), new CommitHash("abcdef1"), null)]);

        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        pullRequestCalls.Should().Be(1);
        bitbucketCalls.Should().Be(1);
        tagCalls.Should().Be(1);
        result.Should().ContainSingle();
        result[0].Resolutions.Should().ContainSingle();
        result[0].Resolutions[0].Merged.Should().NotBeNull();
        result[0].Resolutions[0].Merged!.Version.Should().Be(new ArtifactVersion("2.0.0"));
        result[0].Resolutions[0].Merged!.PullRequest.Id.Should().Be(new PullRequestId(42));
    }

    [Fact(DisplayName = "LoadAsync groups branch-only issues by repository and de-duplicates branch names")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenIssueOnlyHasBranchesGroupsByRepositoryAndDeduplicatesBranchNames()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(103, "QA-103", developmentSummary: /*lang=json,strict*/ """{"branches":3}""");
        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(issue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        jiraDevelopmentClient
            .Setup(client => client.GetBranchesAsync(issue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                TestData.CreateJiraBranchLink(name: "feature/qa-103", repositoryFullName: "workspace/repo-a"),
                TestData.CreateJiraBranchLink(name: "Feature/QA-103", repositoryFullName: "workspace/repo-a"),
                TestData.CreateJiraBranchLink(name: "bugfix/qa-103", repositoryFullName: "workspace/repo-a")
            ]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        result.Should().ContainSingle();
        var resolution = result[0].Resolutions.Should().ContainSingle().Subject;
        resolution.RepositoryFullName.Should().Be(new RepositoryFullName("workspace/repo-a"));
        resolution.RepositorySlug.Should().Be(new RepositorySlug("repo-a"));
        resolution.Merged.Should().BeNull();
        resolution.WithoutMerge.Should().NotBeNull();
        resolution.WithoutMerge!.PullRequests.Should().BeEmpty();
        resolution.WithoutMerge.BranchNames.Should().ContainInOrder(
            new BranchName("bugfix/qa-103"),
            new BranchName("feature/qa-103"));
    }

    [Fact(DisplayName = "LoadAsync skips pull request lookup when development summary reports zero pull requests")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenDevelopmentSummaryReportsZeroPullRequestsLoadsBranchesDirectly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(108, "QA-108", developmentSummary: /*lang=json,strict*/ """{"pullRequests":0,"branches":2}""");
        var pullRequestCalls = 0;
        var branchCalls = 0;

        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetBranchesAsync(issue.Id, It.IsAny<CancellationToken>()))
            .Callback(() => branchCalls++)
            .ReturnsAsync(
            [
                TestData.CreateJiraBranchLink(name: "feature/qa-108", repositoryFullName: "workspace/repo-a"),
                TestData.CreateJiraBranchLink(name: "bugfix/qa-108", repositoryFullName: "workspace/repo-a")
            ]);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(issue.Id, It.IsAny<CancellationToken>()))
            .Callback(() => pullRequestCalls++)
            .ReturnsAsync([]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        pullRequestCalls.Should().Be(0);
        branchCalls.Should().Be(1);
        var resolution = result.Should().ContainSingle().Subject.Resolutions.Should().ContainSingle().Subject;
        resolution.WithoutMerge.Should().NotBeNull();
        resolution.WithoutMerge!.BranchNames.Should().ContainInOrder(
            new BranchName("bugfix/qa-108"),
            new BranchName("feature/qa-108"));
    }

    [Fact(DisplayName = "LoadAsync records merged Jira pull requests without Bitbucket lookup when repository slug is unknown")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenRepositorySlugIsUnknownBuildsMergedFallbackWithoutBitbucketLookup()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(104, "QA-104", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(issue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                TestData.CreateJiraPullRequestLink(
                    id: 77,
                    status: "MERGED",
                    repositoryFullName: "/",
                    destinationBranch: "main",
                    lastUpdatedOn: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero))
            ]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        result.Should().ContainSingle();
        var resolution = result[0].Resolutions.Should().ContainSingle().Subject;
        resolution.RepositoryFullName.Should().Be(new RepositoryFullName("/"));
        resolution.RepositorySlug.Should().Be(RepositorySlug.Unknown);
        resolution.WithoutMerge.Should().BeNull();
        resolution.Merged.Should().NotBeNull();
        resolution.Merged!.Version.Should().Be(ArtifactVersion.NotFound);
        resolution.Merged.PullRequest.RepositoryDisplayName.Should().Be(new RepositoryDisplayName(RepositorySlug.Unknown.Value));
        resolution.Merged.PullRequest.MergeCommitHash.Should().BeNull();
    }

    [Fact(DisplayName = "LoadAsync keeps merged Jira candidates in without-merge when Bitbucket reports a non-target merge")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenBitbucketPullRequestDoesNotMatchTargetBranchKeepsIssueWithoutMerge()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(105, "QA-105", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var jiraPullRequest = TestData.CreateJiraPullRequestLink(
            id: 78,
            status: "MERGED",
            repositoryFullName: "workspace/repo-a",
            sourceBranch: "feature/qa-105",
            destinationBranch: "main",
            lastUpdatedOn: new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero));
        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(issue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([jiraPullRequest]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        bitbucketClient
            .Setup(client => client.GetPullRequestAsync(
                new RepositorySlug("repo-a"),
                new PullRequestId(78),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.CreateBitbucketPullRequest(
                id: 78,
                state: "OPEN",
                sourceBranch: "feature/qa-105",
                destinationBranch: "main"));

        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        var resolution = result.Should().ContainSingle().Subject.Resolutions.Should().ContainSingle().Subject;
        resolution.Merged.Should().BeNull();
        resolution.WithoutMerge.Should().NotBeNull();
        resolution.WithoutMerge!.PullRequests.Should().ContainSingle().Which.Should().Be(jiraPullRequest);
    }

    [Fact(DisplayName = "LoadAsync uses version not found when merged Bitbucket pull request has no merge commit")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncWhenMergedBitbucketPullRequestHasNoMergeCommitReturnsVersionNotFound()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = TestData.CreateIssue(106, "QA-106", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var jiraDevelopmentClient = new Mock<IJiraDevelopmentClient>(MockBehavior.Strict);
        jiraDevelopmentClient
            .Setup(client => client.GetPullRequestsAsync(issue.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                TestData.CreateJiraPullRequestLink(
                    id: 79,
                    status: "MERGED",
                    repositoryFullName: "workspace/repo-a",
                    destinationBranch: "main",
                    lastUpdatedOn: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero))
            ]);

        var bitbucketClient = new Mock<IBitbucketClient>(MockBehavior.Strict);
        bitbucketClient
            .Setup(client => client.GetPullRequestAsync(
                new RepositorySlug("repo-a"),
                new PullRequestId(79),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.CreateBitbucketPullRequest(id: 79, mergeCommitHash: null, updatedOn: new DateTimeOffset(2026, 3, 20, 12, 5, 0, TimeSpan.Zero)));

        var loader = new QaCodeIssueDetailsLoader(
            jiraDevelopmentClient.Object,
            bitbucketClient.Object,
            Options.Create(new ReportOptions { TargetBranch = "main", MaxParallelism = 2 }));

        // Act
        var result = await loader.LoadAsync([issue], progress: null, cts.Token);

        // Assert
        var resolution = result.Should().ContainSingle().Subject.Resolutions.Should().ContainSingle().Subject;
        resolution.Merged.Should().NotBeNull();
        resolution.Merged!.Version.Should().Be(ArtifactVersion.NotFound);
        resolution.Merged.PullRequest.MergeCommitHash.Should().BeNull();
    }
}
