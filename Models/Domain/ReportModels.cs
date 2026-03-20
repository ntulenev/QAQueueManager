namespace QAQueueManager.Models.Domain;

internal sealed record QaIssue(
    long Id,
    string Key,
    string Summary,
    string Status,
    string DevelopmentSummary,
    IReadOnlyList<string> Teams,
    DateTimeOffset? UpdatedAt)
{
    public bool HasCode => !string.IsNullOrWhiteSpace(DevelopmentSummary) && DevelopmentSummary.Trim() != "{}";
}

internal sealed record JiraPullRequestLink(
    int Id,
    string Title,
    string Status,
    string RepositoryFullName,
    string RepositoryUrl,
    string SourceBranch,
    string DestinationBranch,
    string Url,
    DateTimeOffset? LastUpdatedOn);

internal sealed record JiraBranchLink(
    string Name,
    string RepositoryFullName,
    string RepositoryUrl);

internal sealed record BitbucketPullRequest(
    int Id,
    string State,
    string RepositoryFullName,
    string RepositoryDisplayName,
    string RepositorySlug,
    string SourceBranch,
    string DestinationBranch,
    string HtmlUrl,
    string MergeCommitHash,
    DateTimeOffset? UpdatedOn);

internal sealed record BitbucketTag(
    string Name,
    string TargetHash,
    DateTimeOffset? TaggedOn);

internal sealed record QaCodeIssueWithoutMerge(
    QaIssue Issue,
    string RepositoryFullName,
    string RepositorySlug,
    IReadOnlyList<JiraPullRequestLink> PullRequests,
    IReadOnlyList<string> BranchNames);

internal sealed record QaMergedPullRequest(
    int PullRequestId,
    string SourceBranch,
    string DestinationBranch,
    string Version,
    string PullRequestUrl,
    string MergeCommitHash,
    DateTimeOffset? PullRequestUpdatedOn);

internal sealed record QaMergedIssueVersionRow(
    QaIssue Issue,
    string RepositoryFullName,
    string RepositorySlug,
    string Version,
    IReadOnlyList<QaMergedPullRequest> PullRequests,
    bool HasMultipleVersions);

internal sealed record QaRepositorySection(
    string RepositoryFullName,
    string RepositorySlug,
    IReadOnlyList<QaCodeIssueWithoutMerge> WithoutTargetMerge,
    IReadOnlyList<QaMergedIssueVersionRow> MergedIssueRows);

internal sealed record QaTeamSection(
    string Team,
    IReadOnlyList<QaIssue> NoCodeIssues,
    IReadOnlyList<QaRepositorySection> Repositories);

internal sealed record QaQueueReport(
    DateTimeOffset GeneratedAt,
    string Title,
    string Jql,
    string TargetBranch,
    string? TeamGroupingField,
    bool HideNoCodeIssues,
    IReadOnlyList<QaIssue> NoCodeIssues,
    IReadOnlyList<QaRepositorySection> Repositories,
    IReadOnlyList<QaTeamSection> Teams)
{
    public bool IsGroupedByTeam => !string.IsNullOrWhiteSpace(TeamGroupingField);
}

internal enum QaQueueBuildProgressKind
{
    JiraSearchStarted,
    JiraSearchCompleted,
    CodeAnalysisStarted,
    CodeIssueStarted,
    CodeIssueCompleted,
    CodeAnalysisCompleted
}

internal sealed record QaQueueBuildProgress(
    QaQueueBuildProgressKind Kind,
    string? Message = null,
    int Current = 0,
    int Total = 0,
    string? IssueKey = null);
