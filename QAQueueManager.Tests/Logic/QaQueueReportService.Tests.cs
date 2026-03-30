using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using QAQueueManager.Abstractions;
using QAQueueManager.Logic;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Logic;

public sealed class QaQueueReportServiceTests
{
    [Fact(DisplayName = "BuildAsync builds repository sections when team grouping is disabled")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenTeamGroupingIsDisabledBuildsRepositorySections()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var progressReports = new List<QaQueueBuildProgress>();
        var progress = new Progress<QaQueueBuildProgress>(progressReports.Add);
        var noCodeIssue = TestData.CreateIssue(1001, "QA-1", summary: "No code", developmentSummary: /*lang=json,strict*/ """{}""");
        var codeIssue = TestData.CreateIssue(1002, "QA-2", summary: "Has code", status: "In QA", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var searchCalls = 0;
        var loadCalls = 0;

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => searchCalls++)
            .ReturnsAsync([noCodeIssue, codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == progress),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback<IReadOnlyList<QaIssue>, IProgress<QaQueueBuildProgress>?, CancellationToken>((_, reporter, _) =>
            {
                loadCalls++;
                reporter!.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.CodeAnalysisStarted, "Loader callback", 0, 1));
            })
            .ReturnsAsync(
            [
                new ProcessedCodeIssue(
                    codeIssue,
                    [
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-a"),
                            new RepositorySlug("repo-a"),
                            new IssueWithoutMergeData([], [new BranchName("feature/qa-2")]),
                            null)
                    ])
            ]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress, cts.Token);

        // Assert
        searchCalls.Should().Be(1);
        loadCalls.Should().Be(1);
        report.IsGroupedByTeam.Should().BeFalse();
        report.Title.Should().Be("QA Queue");
        report.Jql.Should().Be("project = QA");
        report.TargetBranch.Should().Be(new BranchName("main"));
        report.NoCodeIssues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-1"));
        report.Repositories.Should().ContainSingle();
        report.Repositories[0].RepositoryFullName.Should().Be(new RepositoryFullName("workspace/repo-a"));
        report.Repositories[0].WithoutTargetMerge.Should().ContainSingle();
        report.Repositories[0].WithoutTargetMerge[0].BranchNames.Should().ContainSingle().Which.Should().Be(new BranchName("feature/qa-2"));
        report.Teams.Should().BeEmpty();
        progressReports.Should().Contain(reportUpdate => reportUpdate.Kind == QaQueueBuildProgressKind.JiraSearchStarted);
        progressReports.Should().Contain(reportUpdate => reportUpdate.Kind == QaQueueBuildProgressKind.JiraSearchCompleted);
        progressReports.Should().Contain(reportUpdate => reportUpdate.Message == "Loader callback");
    }

    [Fact(DisplayName = "BuildAsync groups issues into team sections when team field is configured")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenTeamGroupingIsEnabledBuildsTeamSections()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var noCodeIssue = TestData.CreateIssue(2001, "QA-10", developmentSummary: /*lang=json,strict*/ """{}""", teams: [new TeamName("Core")]);
        var codeIssue = TestData.CreateIssue(2002, "QA-11", status: "In QA", developmentSummary: /*lang=json,strict*/ """{"branches":1}""", teams: [new TeamName("Core")]);
        var searchCalls = 0;
        var loadCalls = 0;

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => searchCalls++)
            .ReturnsAsync([noCodeIssue, codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => loadCalls++)
            .ReturnsAsync(
            [
                new ProcessedCodeIssue(
                    codeIssue,
                    [
                new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-b"),
                            new RepositorySlug("repo-b"),
                            new IssueWithoutMergeData([], [new BranchName("feature/qa-11")]),
                            null)
                    ])
            ]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(teamField: "Team"), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        searchCalls.Should().Be(1);
        loadCalls.Should().Be(1);
        report.IsGroupedByTeam.Should().BeTrue();
        report.Repositories.Should().BeEmpty();
        report.Teams.Should().ContainSingle();
        report.Teams[0].Team.Should().Be(new TeamName("Core"));
        report.Teams[0].NoCodeIssues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-10"));
        report.Teams[0].Repositories.Should().ContainSingle();
        report.Teams[0].Repositories[0].RepositorySlug.Should().Be(new RepositorySlug("repo-b"));
    }

    [Fact(DisplayName = "BuildAsync hides team sections with only no-code issues when configured")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenHideNoCodeIssuesIsEnabledSkipsTeamsWithoutRepositories()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var noCodeIssue = TestData.CreateIssue(3001, "QA-20", developmentSummary: /*lang=json,strict*/ """{}""", teams: [new TeamName("Core")]);

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([noCodeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 0),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(teamField: "Team"), CreateReportOptions(hideNoCodeIssues: true)));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        report.IsGroupedByTeam.Should().BeTrue();
        report.NoCodeIssues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-20"));
        report.Teams.Should().BeEmpty();
    }

    [Fact(DisplayName = "BuildAsync treats issues with explicit zero pull requests and branches as no-code issues")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenIssueExplicitlyReportsNoDevelopmentDoesNotSendItToCodeLoader()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var noDevelopmentIssue = TestData.CreateIssue(
            3007,
            "QA-26",
            developmentSummary: /*lang=json,strict*/ """{"pullRequests":0,"branches":0}""");

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([noDevelopmentIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 0),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        report.NoCodeIssues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-26"));
        report.Repositories.Should().BeEmpty();
        report.Teams.Should().BeEmpty();
    }

    [Fact(DisplayName = "BuildAsync skips empty team sections when processed issues yield no repository entries")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenProcessedIssueHasNoResolutionsSkipsEmptyTeamSection()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var codeIssue = TestData.CreateIssue(3002, "QA-21", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""", teams: [new TeamName("Core")]);

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([new ProcessedCodeIssue(codeIssue, [])]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(teamField: "Team"), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        report.IsGroupedByTeam.Should().BeTrue();
        report.NoCodeIssues.Should().BeEmpty();
        report.Teams.Should().BeEmpty();
    }

    [Fact(DisplayName = "BuildAsync builds merged repository rows when code issues resolve target branch merges")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenIssueHasMergedRepositoriesBuildsMergedRows()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var codeIssue = TestData.CreateIssue(3003, "QA-22", status: "In QA", developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""");
        var pullRequest = TestData.CreateBitbucketPullRequest(
            id: 88,
            repositoryFullName: "workspace/repo-c",
            repositoryDisplayName: "Repo C",
            repositorySlug: "repo-c",
            sourceBranch: "feature/qa-22",
            destinationBranch: "main",
            updatedOn: new DateTimeOffset(2026, 3, 20, 13, 0, 0, TimeSpan.Zero));

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(
            [
                new ProcessedCodeIssue(
                    codeIssue,
                    [
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-c"),
                            new RepositorySlug("repo-c"),
                            null,
                            new MergedIssueData(pullRequest, new ArtifactVersion("2.3.4")))
                    ])
            ]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        report.IsGroupedByTeam.Should().BeFalse();
        report.Repositories.Should().ContainSingle();
        report.Repositories[0].WithoutTargetMerge.Should().BeEmpty();
        report.Repositories[0].MergedIssueRows.Should().ContainSingle();
        report.Repositories[0].MergedIssueRows[0].Issue.Key.Should().Be(new JiraIssueKey("QA-22"));
        report.Repositories[0].MergedIssueRows[0].Version.Should().Be(new ArtifactVersion("2.3.4"));
        report.Repositories[0].MergedIssueRows[0].PullRequests.Should().ContainSingle().Which.PullRequestId.Should().Be(new PullRequestId(88));
    }

    [Fact(DisplayName = "BuildAsync groups multiple merged resolutions from one repository into multiple version rows")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenIssueHasMultipleMergedResolutionsInSameRepositoryBuildsMultipleVersionRows()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var codeIssue = TestData.CreateIssue(3004, "QA-23", status: "In QA", developmentSummary: /*lang=json,strict*/ """{"pullRequests":2}""");
        var pullRequestA = TestData.CreateBitbucketPullRequest(
            id: 89,
            repositoryFullName: "workspace/repo-c",
            repositoryDisplayName: "Repo C",
            repositorySlug: "repo-c",
            sourceBranch: "feature/qa-23-first",
            destinationBranch: "main",
            updatedOn: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
        var pullRequestB = TestData.CreateBitbucketPullRequest(
            id: 90,
            repositoryFullName: "workspace/repo-c",
            repositoryDisplayName: "Repo C",
            repositorySlug: "repo-c",
            sourceBranch: "feature/qa-23-fix",
            destinationBranch: "main",
            updatedOn: new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero));

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(
            [
                new ProcessedCodeIssue(
                    codeIssue,
                    [
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-c"),
                            new RepositorySlug("repo-c"),
                            null,
                            new MergedIssueData(pullRequestA, new ArtifactVersion("1.0.1"))),
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-c"),
                            new RepositorySlug("repo-c"),
                            null,
                            new MergedIssueData(pullRequestB, new ArtifactVersion("1.0.5")))
                    ])
            ]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        var repository = report.Repositories.Should().ContainSingle().Subject;
        repository.MergedIssueRows.Should().HaveCount(2);
        repository.MergedIssueRows.Select(static row => row.Version.Value).Should().ContainInOrder("1.0.1", "1.0.5");
        repository.MergedIssueRows.All(static row => row.HasDuplicateIssue).Should().BeTrue();
        repository.MergedIssueRows[0].PullRequests.Should().ContainSingle().Which.PullRequestId.Should().Be(new PullRequestId(89));
        repository.MergedIssueRows[1].PullRequests.Should().ContainSingle().Which.PullRequestId.Should().Be(new PullRequestId(90));
    }

    [Fact(DisplayName = "BuildAsync raises alert when one issue appears in multiple repositories")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenIssueAppearsInMultipleRepositoriesMarksDuplicateIssueAlerts()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var codeIssue = TestData.CreateIssue(3005, "QA-24", status: "In QA", developmentSummary: /*lang=json,strict*/ """{"pullRequests":2}""");
        var pullRequestA = TestData.CreateBitbucketPullRequest(id: 101, repositoryFullName: "workspace/repo-a", repositorySlug: "repo-a");
        var pullRequestB = TestData.CreateBitbucketPullRequest(id: 102, repositoryFullName: "workspace/repo-b", repositoryDisplayName: "Repo B", repositorySlug: "repo-b");

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(
            [
                new ProcessedCodeIssue(
                    codeIssue,
                    [
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-a"),
                            new RepositorySlug("repo-a"),
                            null,
                            new MergedIssueData(pullRequestA, new ArtifactVersion("2.0.0"))),
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-b"),
                            new RepositorySlug("repo-b"),
                            null,
                            new MergedIssueData(pullRequestB, new ArtifactVersion("3.0.0")))
                    ])
            ]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        report.Repositories.Should().HaveCount(2);
        report.Repositories.SelectMany(static repository => repository.MergedIssueRows)
            .Should()
            .OnlyContain(static row => row.HasDuplicateIssue);
    }

    [Fact(DisplayName = "BuildAsync raises alert when one issue appears in merged and without-merge sections")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncWhenIssueAppearsInMergedAndWithoutMergeMarksDuplicateIssueAlerts()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var codeIssue = TestData.CreateIssue(3006, "QA-25", status: "In QA", developmentSummary: /*lang=json,strict*/ """{"pullRequests":2}""");
        var mergedPullRequest = TestData.CreateBitbucketPullRequest(id: 103, repositoryFullName: "workspace/repo-a", repositorySlug: "repo-a");
        var nonMergedBranch = new BranchName("feature/qa-25");

        var jiraIssueSearchClient = new Mock<IJiraIssueSearchClient>(MockBehavior.Strict);
        jiraIssueSearchClient
            .Setup(client => client.SearchIssuesAsync(It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync([codeIssue]);

        var codeIssueDetailsLoader = new Mock<IQaCodeIssueDetailsLoader>(MockBehavior.Strict);
        codeIssueDetailsLoader
            .Setup(loader => loader.LoadAsync(
                It.Is<IReadOnlyList<QaIssue>>(issues => issues.Count == 1 && issues[0] == codeIssue),
                It.Is<IProgress<QaQueueBuildProgress>?>(reporter => reporter == null),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(
            [
                new ProcessedCodeIssue(
                    codeIssue,
                    [
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-a"),
                            new RepositorySlug("repo-a"),
                            null,
                            new MergedIssueData(mergedPullRequest, new ArtifactVersion("4.0.0"))),
                        new RepositoryResolution(
                            new RepositoryFullName("workspace/repo-b"),
                            new RepositorySlug("repo-b"),
                            new IssueWithoutMergeData([], [nonMergedBranch]),
                            null)
                    ])
            ]);

        var service = new QaQueueReportService(
            jiraIssueSearchClient.Object,
            codeIssueDetailsLoader.Object,
            CreateReportBuilder(CreateJiraOptions(), CreateReportOptions()));

        // Act
        var report = await service.BuildAsync(progress: null, cts.Token);

        // Assert
        report.Repositories.Should().HaveCount(2);
        report.Repositories.Single(static repository => repository.RepositorySlug == new RepositorySlug("repo-a"))
            .MergedIssueRows.Should().ContainSingle().Which.HasDuplicateIssue.Should().BeTrue();
        report.Repositories.Single(static repository => repository.RepositorySlug == new RepositorySlug("repo-b"))
            .WithoutTargetMerge.Should().ContainSingle().Which.HasDuplicateIssue.Should().BeTrue();
    }

    private static JiraOptions CreateJiraOptions(string teamField = "")
    {
        return new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/", UriKind.Absolute),
            Email = "qa@example.test",
            ApiToken = "token",
            Jql = "project = QA",
            DevelopmentField = "development",
            TeamField = teamField,
            MaxResultsPerPage = 50,
            RetryCount = 0,
            BitbucketApplicationType = "bitbucket",
            PullRequestDataType = "pullrequest",
            BranchDataType = "branch"
        };
    }

    private static ReportOptions CreateReportOptions(bool hideNoCodeIssues = false)
    {
        return new ReportOptions
        {
            Title = "QA Queue",
            TargetBranch = "main",
            PdfOutputPath = "qa-queue-report.pdf",
            ExcelOutputPath = "qa-queue-report.xlsx",
            MaxParallelism = 2,
            HideNoCodeIssues = hideNoCodeIssues,
            OpenAfterGeneration = false
        };
    }

    private static QaQueueReportBuilder CreateReportBuilder(JiraOptions jiraOptions, ReportOptions reportOptions)
    {
        return new QaQueueReportBuilder(
            Options.Create(jiraOptions),
            Options.Create(reportOptions));
    }
}


