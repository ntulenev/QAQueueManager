using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Builds the shared presentation document for console and PDF rendering.
/// </summary>
internal sealed class QaQueueReportDocumentBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueReportDocumentBuilder"/> class.
    /// </summary>
    /// <param name="jiraOptions">The Jira configuration used to build issue browse links.</param>
    public QaQueueReportDocumentBuilder(IOptions<JiraOptions> jiraOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraOptions);

        _jiraBrowseBaseUrl = new Uri(jiraOptions.Value.BaseUrl.ToString().TrimEnd('/') + "/browse/", UriKind.Absolute);
    }

    /// <summary>
    /// Maps a domain report into the shared presentation document consumed by renderers.
    /// </summary>
    /// <param name="report">The domain report to map.</param>
    /// <returns>The shared presentation document.</returns>
    public QaQueuePresentationDocument Build(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var repositoryCount = report.IsGroupedByTeam
            ? report.Teams.Sum(static team => team.Repositories.Count)
            : report.Repositories.Count;

        var header = new QaQueuePresentationDocumentHeader(
            report.Title,
            QaQueuePresentationFormatting.FormatReportTimestamp(report.GeneratedAt),
            report.TargetBranch.Value,
            report.Jql,
            report.TeamGroupingField,
            repositoryCount,
            report.NoCodeIssues.Count,
            report.IsGroupedByTeam ? report.Teams.Count : 0);

        var noCodeIssues = report.NoCodeIssues
            .Select((issue, index) => BuildNoCodeIssueRow(index + 1, issue))
            .ToArray();

        if (report.IsGroupedByTeam)
        {
            return new QaQueuePresentationDocument(
                header,
                report.HideNoCodeIssues,
                noCodeIssues,
                [],
                [.. report.Teams.Select(BuildTeamSection)]);
        }

        return new QaQueuePresentationDocument(
            header,
            report.HideNoCodeIssues,
            noCodeIssues,
            [.. report.Repositories.Select(BuildRepositorySection)],
            []);
    }

    private QaQueuePresentationTeamSection BuildTeamSection(QaTeamSection team) =>
        new(
            team.Team.Value,
            [.. team.NoCodeIssues.Select((issue, index) => BuildNoCodeIssueRow(index + 1, issue))],
            [.. team.Repositories.Select(BuildRepositorySection)]);

    private QaQueuePresentationRepositorySection BuildRepositorySection(QaRepositorySection repository) =>
        new(
            repository.RepositoryFullName.Value,
            [.. repository.WithoutTargetMerge.Select((item, index) => BuildWithoutMergeRow(index + 1, item))],
            [.. repository.MergedIssueRows.Select((item, index) => BuildMergedRow(index + 1, item))]);

    private QaQueuePresentationNoCodeIssueRow BuildNoCodeIssueRow(int index, QaIssue issue) =>
        new(
            index,
            BuildIssueRef(issue.Key, highlight: false),
            issue.Status.Value,
            issue.Assignee,
            QaQueuePresentationFormatting.FormatIssueTimestamp(issue.UpdatedAt),
            issue.Summary);

    private QaQueuePresentationWithoutMergeRow BuildWithoutMergeRow(int index, QaCodeIssueWithoutMerge item) =>
        new(
            index,
            BuildIssueRef(item.Issue.Key, highlight: false),
            item.Issue.Status.Value,
            item.Issue.Assignee,
            QaQueuePresentationFormatting.FormatPullRequests(item.PullRequests),
            QaQueuePresentationFormatting.FormatBranchNames(item.BranchNames),
            QaQueuePresentationFormatting.FormatAlertText(item.HasDuplicateIssue),
            QaQueuePresentationFormatting.FormatIssueTimestamp(item.Issue.UpdatedAt),
            item.Issue.Summary);

    private QaQueuePresentationMergedIssueRow BuildMergedRow(int index, QaMergedIssueVersionRow item) =>
        new(
            index,
            BuildIssueRef(item.Issue.Key, item.HasDuplicateIssue),
            item.Issue.Status.Value,
            item.Issue.Assignee,
            QaQueuePresentationFormatting.FormatMergedPullRequests(item.PullRequests),
            item.Version.Value,
            QaQueuePresentationFormatting.FormatAlertText(item.HasDuplicateIssue),
            QaQueuePresentationFormatting.FormatBranchNames(item.PullRequests.Select(static pr => pr.SourceBranch)),
            QaQueuePresentationFormatting.FormatBranchNames(item.PullRequests.Select(static pr => pr.DestinationBranch)),
            QaQueuePresentationFormatting.FormatIssueTimestamp(item.Issue.UpdatedAt),
            item.Issue.Summary);

    private QaQueuePresentationIssueRef BuildIssueRef(JiraIssueKey issueKey, bool highlight) =>
        new(issueKey.Value, QaQueuePresentationFormatting.BuildIssueUrl(_jiraBrowseBaseUrl, issueKey), highlight);

    private readonly Uri _jiraBrowseBaseUrl;
}
