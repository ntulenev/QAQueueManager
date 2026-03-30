using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Testing;

internal static class TestData
{
    public static QaIssue CreateIssue(
        int id = 1001,
        string key = "QA-1",
        string summary = "Summary",
        string status = "Open",
        string assignee = "QA Engineer",
        string developmentSummary = /*lang=json,strict*/ """{}""",
        IReadOnlyList<TeamName>? teams = null,
        DateTimeOffset? updatedAt = null)
    {
        return new QaIssue(
            new JiraIssueId(id),
            new JiraIssueKey(key),
            summary,
            new JiraIssueStatus(status),
            assignee,
            developmentSummary,
            teams ?? [],
            updatedAt);
    }

    public static JiraPullRequestLink CreateJiraPullRequestLink(
        int id = 101,
        string title = "PR-101",
        string status = "OPEN",
        string repositoryFullName = "workspace/repo-a",
        string sourceBranch = "feature/qa-1",
        string destinationBranch = "main",
        string? url = "https://bitbucket.example.test/workspace/repo-a/pull-requests/101",
        string? repositoryUrl = "https://bitbucket.example.test/workspace/repo-a",
        DateTimeOffset? lastUpdatedOn = null)
    {
        return new JiraPullRequestLink(
            new PullRequestId(id),
            title,
            new PullRequestState(status),
            new RepositoryFullName(repositoryFullName),
            string.IsNullOrWhiteSpace(repositoryUrl) ? null : new Uri(repositoryUrl, UriKind.Absolute),
            new BranchName(sourceBranch),
            new BranchName(destinationBranch),
            string.IsNullOrWhiteSpace(url) ? null : new Uri(url, UriKind.Absolute),
            lastUpdatedOn);
    }

    public static JiraBranchLink CreateJiraBranchLink(
        string name = "feature/qa-1",
        string repositoryFullName = "workspace/repo-a",
        string? repositoryUrl = "https://bitbucket.example.test/workspace/repo-a")
    {
        return new JiraBranchLink(
            new BranchName(name),
            new RepositoryFullName(repositoryFullName),
            string.IsNullOrWhiteSpace(repositoryUrl) ? null : new Uri(repositoryUrl, UriKind.Absolute));
    }

    public static BitbucketPullRequest CreateBitbucketPullRequest(
        int id = 101,
        string state = "MERGED",
        string repositoryFullName = "workspace/repo-a",
        string repositoryDisplayName = "Repo A",
        string repositorySlug = "repo-a",
        string sourceBranch = "feature/qa-1",
        string destinationBranch = "main",
        string? htmlUrl = "https://bitbucket.example.test/workspace/repo-a/pull-requests/101",
        string? mergeCommitHash = "abcdef1",
        DateTimeOffset? updatedOn = null)
    {
        return new BitbucketPullRequest(
            new PullRequestId(id),
            new PullRequestState(state),
            new RepositoryFullName(repositoryFullName),
            new RepositoryDisplayName(repositoryDisplayName),
            new RepositorySlug(repositorySlug),
            new BranchName(sourceBranch),
            new BranchName(destinationBranch),
            string.IsNullOrWhiteSpace(htmlUrl) ? null : new Uri(htmlUrl, UriKind.Absolute),
            string.IsNullOrWhiteSpace(mergeCommitHash) ? null : new CommitHash(mergeCommitHash),
            updatedOn);
    }

    public static QaMergedPullRequest CreateMergedPullRequest(
        int id = 101,
        string sourceBranch = "feature/qa-1",
        string destinationBranch = "main",
        string version = "1.2.3",
        string? url = "https://bitbucket.example.test/workspace/repo-a/pull-requests/101",
        string? mergeCommitHash = "abcdef1",
        DateTimeOffset? updatedOn = null)
    {
        return new QaMergedPullRequest(
            new PullRequestId(id),
            new BranchName(sourceBranch),
            new BranchName(destinationBranch),
            new ArtifactVersion(version),
            string.IsNullOrWhiteSpace(url) ? null : new Uri(url, UriKind.Absolute),
            string.IsNullOrWhiteSpace(mergeCommitHash) ? null : new CommitHash(mergeCommitHash),
            updatedOn);
    }

    public static QaQueueReport CreateReport(bool groupedByTeam = false, bool hideNoCodeIssues = false)
    {
        var noCodeIssue = CreateIssue(id: 1001, key: "QA-1", summary: "No code", teams: [new TeamName("Core")]);
        var mergedIssue = CreateIssue(
            id: 1002,
            key: "QA-2",
            summary: "Merged",
            status: "In QA",
            developmentSummary: /*lang=json,strict*/ """{"pullRequests":1}""",
            teams: [new TeamName("Core")],
            updatedAt: new DateTimeOffset(2026, 3, 20, 9, 30, 0, TimeSpan.Zero));

        var repository = new QaRepositorySection(
            new RepositoryFullName("workspace/repo-a"),
            new RepositorySlug("repo-a"),
            [
                new QaCodeIssueWithoutMerge(
                    mergedIssue,
                    new RepositoryFullName("workspace/repo-a"),
                    new RepositorySlug("repo-a"),
                    [CreateJiraPullRequestLink()],
                    [new BranchName("feature/qa-2")])
            ],
            [
                new QaMergedIssueVersionRow(
                    mergedIssue,
                    new RepositoryFullName("workspace/repo-a"),
                    new RepositorySlug("repo-a"),
                    new ArtifactVersion("1.2.3"),
                    [CreateMergedPullRequest()],
                    HasMultipleVersions: true)
            ]);

        IReadOnlyList<QaTeamSection> teams = groupedByTeam
            ? [
                new QaTeamSection(
                    new TeamName("Core"),
                    [noCodeIssue],
                    [repository])
            ]
            : [];
        IReadOnlyList<QaRepositorySection> repositories = groupedByTeam ? [] : [repository];

        return new QaQueueReport(
            new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
            "QA Queue",
            "project = QA",
            new BranchName("main"),
            groupedByTeam ? "Team" : null,
            hideNoCodeIssues,
            [noCodeIssue],
            repositories,
            teams);
    }
}
