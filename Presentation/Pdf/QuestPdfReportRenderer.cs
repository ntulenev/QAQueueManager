using System.Globalization;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QAQueueManager.Presentation.Pdf;

/// <summary>
/// Renders the QA queue report as a PDF document using QuestPDF.
/// </summary>
internal sealed class QuestPdfReportRenderer : IPdfReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestPdfReportRenderer"/> class.
    /// </summary>
    /// <param name="jiraOptions">The Jira configuration options used to build issue links.</param>
    public QuestPdfReportRenderer(IOptions<JiraOptions> jiraOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraOptions);

        _jiraIssueBaseUrl = new Uri(jiraOptions.Value.BaseUrl.ToString().TrimEnd('/') + "/browse/", UriKind.Absolute);
    }

    /// <summary>
    /// Renders the supplied report to PDF bytes.
    /// </summary>
    /// <param name="report">The report to render.</param>
    /// <returns>The generated PDF bytes.</returns>
    public byte[] Render(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var repositoryCount = report.IsGroupedByTeam
            ? report.Teams.Sum(static team => team.Repositories.Count)
            : report.Repositories.Count;

        return Document.Create(container =>
        {
            _ = container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(static style => style.FontSize(10));

                page.Header().Column(column =>
                {
                    _ = column.Item().Text(report.Title).Bold().FontSize(18);
                    _ = column.Item().Text($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
                    _ = column.Item().Text($"Target branch: {report.TargetBranch.Value}");
                    _ = column.Item().Text($"JQL: {report.Jql}");
                    _ = column.Item().Text($"Totals: no-code={report.NoCodeIssues.Count}, repos={repositoryCount}, hide-no-code={report.HideNoCodeIssues}");
                    if (report.IsGroupedByTeam)
                    {
                        _ = column.Item().Text($"Grouping: by team field {report.TeamGroupingField}");
                        _ = column.Item().Text($"Teams: {report.Teams.Count}");
                    }
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    if (report.IsGroupedByTeam)
                    {
                        ComposeTeamSections(column, report);
                    }
                    else
                    {
                        if (!report.HideNoCodeIssues)
                        {
                            ComposeNoCodeSection(column, "QA tasks without code", report.NoCodeIssues);
                        }

                        foreach (var repository in report.Repositories)
                        {
                            ComposeRepositorySection(column, repository);
                        }
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    _ = text.Span("Page ");
                    _ = text.CurrentPageNumber();
                    _ = text.Span(" / ");
                    _ = text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private void ComposeTeamSections(ColumnDescriptor column, QaQueueReport report)
    {
        foreach (var team in report.Teams)
        {
            _ = column.Item().PaddingTop(4).Text($"Team: {team.Team.Value}").Bold().FontSize(15);

            if (!report.HideNoCodeIssues)
            {
                ComposeNoCodeSection(column, "QA tasks without code", team.NoCodeIssues);
            }

            foreach (var repository in team.Repositories)
            {
                ComposeRepositorySection(column, repository);
            }
        }
    }

    private void ComposeNoCodeSection(
        ColumnDescriptor column,
        string title,
        IReadOnlyList<QaIssue> issues)
    {
        _ = column.Item().Text(title).Bold().FontSize(14);

        if (issues.Count == 0)
        {
            _ = column.Item().Text("No QA tasks without code.").FontColor(Colors.Grey.Darken1);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(3.2f);
            });

            ComposeHeader(table, "Issue", "Status", "Last updated", "Summary");

            for (var index = 0; index < issues.Count; index++)
            {
                var issue = issues[index];
                ComposeIssueRow(table, index + 1, issue, issue.Status.Value, FormatDate(issue.UpdatedAt), issue.Summary);
            }
        });
    }

    private void ComposeRepositorySection(ColumnDescriptor column, QaRepositorySection repository)
    {
        _ = column.Item().PaddingTop(4).Text(repository.RepositoryFullName.Value).Bold().FontSize(14);

        if (repository.WithoutTargetMerge.Count > 0)
        {
            _ = column.Item().Text("Tasks without merge into target branch").SemiBold().FontColor(Colors.Orange.Darken2);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(3.2f);
                });

                ComposeHeader(table, "Issue", "Status", "PRs", "Branches", "Last updated", "Summary");

                for (var index = 0; index < repository.WithoutTargetMerge.Count; index++)
                {
                    var item = repository.WithoutTargetMerge[index];
                    ComposeIssueRow(
                        table,
                        index + 1,
                        item.Issue,
                        item.Issue.Status.Value,
                        string.Join(", ", item.PullRequests.Select(static pr => $"#{pr.Id}:{pr.Status.Value}->{pr.DestinationBranch.Value}")),
                        FormatBranchNames(item.BranchNames),
                        FormatDate(item.Issue.UpdatedAt),
                        item.Issue.Summary);
                }
            });
        }

        if (repository.MergedIssueRows.Count == 0)
        {
            return;
        }

        _ = column.Item().Text("Tasks merged into target branch").SemiBold().FontColor(Colors.Green.Darken2);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(0.9f);
                columns.RelativeColumn(0.9f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(2.5f);
            });

            ComposeHeader(table, "Issue", "Status", "PRs", "Artifact version", "Alert", "Source", "Target", "Last updated", "Summary");

            for (var index = 0; index < repository.MergedIssueRows.Count; index++)
            {
                var item = repository.MergedIssueRows[index];
                ComposeIssueRow(
                    table,
                    index + 1,
                    item.Issue,
                    item.HasMultipleVersions,
                    item.Issue.Status.Value,
                    FormatMergedPullRequests(item.PullRequests),
                    item.Version.Value,
                    FormatAlertText(item),
                    FormatBranchNames(item.PullRequests.Select(static pr => pr.SourceBranch)),
                    FormatBranchNames(item.PullRequests.Select(static pr => pr.DestinationBranch)),
                    FormatDate(item.Issue.UpdatedAt),
                    item.Issue.Summary);
            }
        });
    }

    private static void ComposeHeader(TableDescriptor table, params string[] columns)
    {
        table.Header(header =>
        {
            _ = header.Cell().Element(StyleHeaderCell).Text("#");
            foreach (var column in columns)
            {
                _ = header.Cell().Element(StyleHeaderCell).Text(column);
            }
        });
    }

    private void ComposeIssueRow(TableDescriptor table, int index, QaIssue issue, params string[] values) =>
        ComposeIssueRow(table, index, issue, false, values);

    private void ComposeIssueRow(TableDescriptor table, int index, QaIssue issue, bool highlightIssue, params string[] values)
    {
        _ = table.Cell().Element(StyleBodyCell).Text(index.ToString(CultureInfo.InvariantCulture));
        table.Cell().Element(StyleBodyCell).Text(text =>
        {
            var hyperlink = text.Hyperlink(issue.Key.Value, BuildIssueUrl(issue.Key)).Underline();
            _ = highlightIssue
                ? hyperlink.FontColor(Colors.Orange.Darken2).SemiBold()
                : hyperlink.FontColor(Colors.Blue.Darken2);
        });

        foreach (var value in values)
        {
            var textColor = string.Equals(value, MULTI_VERSION_ALERT_TEXT, StringComparison.Ordinal)
                ? Colors.Orange.Darken2
                : Colors.Black;
            var isAlert = string.Equals(value, MULTI_VERSION_ALERT_TEXT, StringComparison.Ordinal);
            table.Cell().Element(StyleBodyCell).Text(text =>
            {
                var span = text.Span(string.IsNullOrWhiteSpace(value) ? "-" : value).FontColor(textColor);
                if (isAlert)
                {
                    _ = span.SemiBold();
                }
            });
        }
    }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";

    private static string FormatMergedPullRequests(IReadOnlyList<QaMergedPullRequest> pullRequests) => pullRequests.Count == 0 ? "-" : string.Join(", ", pullRequests.Select(static pr => $"#{pr.PullRequestId}"));

    private static string FormatBranchNames(IEnumerable<BranchName> branchNames)
    {
        var values = branchNames
            .Select(static branch => branch.Value)
            .Where(static branch => !string.IsNullOrWhiteSpace(branch))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? "-" : string.Join(", ", values);
    }

    private static IContainer StyleHeaderCell(IContainer container)
        => container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten3)
            .Padding(4);

    private static IContainer StyleBodyCell(IContainer container)
        => container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4);

    private string BuildIssueUrl(JiraIssueKey issueKey) =>
        new Uri(_jiraIssueBaseUrl, Uri.EscapeDataString(issueKey.Value)).ToString();

    private static string FormatAlertText(QaMergedIssueVersionRow item) =>
        item.HasMultipleVersions ? MULTI_VERSION_ALERT_TEXT : "-";

    private const string MULTI_VERSION_ALERT_TEXT = "MULTI-VERSION";
    private readonly Uri _jiraIssueBaseUrl;
}
