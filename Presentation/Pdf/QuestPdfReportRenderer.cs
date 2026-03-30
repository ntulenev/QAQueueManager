using System.Globalization;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation.Shared;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QAQueueManager.Presentation.Pdf;

/// <summary>
/// Renders the QA queue report as a PDF document using QuestPDF.
/// </summary>
internal sealed class QuestPdfReportRenderer : IPdfReportRenderer
{
    public QuestPdfReportRenderer(QaQueueReportDocumentBuilder documentBuilder)
    {
        ArgumentNullException.ThrowIfNull(documentBuilder);

        _documentBuilder = documentBuilder;
    }

    /// <inheritdoc />
    public byte[] Render(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var document = _documentBuilder.Build(report);
        var header = document.Header;

        return Document.Create(container =>
        {
            _ = container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(static style => style.FontSize(10));

                page.Header().Column(column =>
                {
                    _ = column.Item().Text(header.Title).Bold().FontSize(18);
                    _ = column.Item().Text($"Generated: {header.GeneratedAt}");
                    _ = column.Item().Text($"Target branch: {header.TargetBranch}");
                    _ = column.Item().Text($"JQL: {header.Jql}");
                    _ = column.Item().Text($"Totals: no-code={header.NoCodeIssueCount}, repos={header.RepositoryCount}, hide-no-code={document.HideNoCodeIssues}");
                    if (document.IsGroupedByTeam)
                    {
                        _ = column.Item().Text($"Grouping: by team field {header.TeamGroupingField}");
                        _ = column.Item().Text($"Teams: {header.TeamCount}");
                    }
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    if (document.IsGroupedByTeam)
                    {
                        ComposeTeamSections(column, document);
                    }
                    else
                    {
                        if (!document.HideNoCodeIssues)
                        {
                            ComposeNoCodeSection(column, "QA tasks without code", document.NoCodeIssues);
                        }

                        foreach (var repository in document.Repositories)
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

    private static void ComposeTeamSections(ColumnDescriptor column, QaQueuePresentationDocument document)
    {
        foreach (var team in document.Teams)
        {
            _ = column.Item().PaddingTop(4).Text($"Team: {team.TeamName}").Bold().FontSize(15);

            if (!document.HideNoCodeIssues)
            {
                ComposeNoCodeSection(column, "QA tasks without code", team.NoCodeIssues);
            }

            foreach (var repository in team.Repositories)
            {
                ComposeRepositorySection(column, repository);
            }
        }
    }

    private static void ComposeNoCodeSection(
        ColumnDescriptor column,
        string title,
        IReadOnlyList<QaQueuePresentationNoCodeIssueRow> issues)
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
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(3.2f);
            });

            ComposeHeader(table, "Issue", "Status", "Assignee", "Last updated", "Summary");

            foreach (var issue in issues)
            {
                ComposeIssueRow(table, issue.Index, issue.Issue, issue.Status, issue.Assignee, issue.LastUpdated, issue.Summary);
            }
        });
    }

    private static void ComposeRepositorySection(ColumnDescriptor column, QaQueuePresentationRepositorySection repository)
    {
        _ = column.Item().PaddingTop(4).Text(repository.RepositoryName).Bold().FontSize(14);

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
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(3.2f);
                });

                ComposeHeader(table, "Issue", "Status", "Assignee", "PRs", "Branches", "Alert", "Last updated", "Summary");

                foreach (var item in repository.WithoutTargetMerge)
                {
                    ComposeIssueRow(
                        table,
                        item.Index,
                        item.Issue,
                        item.Status,
                        item.Assignee,
                        item.PullRequests,
                        item.Branches,
                        item.Alert,
                        item.LastUpdated,
                        item.Summary);
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
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(2.5f);
            });

            ComposeHeader(table, "Issue", "Status", "Assignee", "PRs", "Artifact version", "Alert", "Source", "Target", "Last updated", "Summary");

            foreach (var item in repository.MergedIssueRows)
            {
                ComposeIssueRow(
                    table,
                    item.Index,
                    item.Issue,
                    item.Status,
                    item.Assignee,
                    item.PullRequests,
                    item.ArtifactVersion,
                    item.Alert,
                    item.Source,
                    item.Target,
                    item.LastUpdated,
                    item.Summary);
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

    private static void ComposeIssueRow(TableDescriptor table, int index, QaQueuePresentationIssueRef issue, params string[] values)
    {
        _ = table.Cell().Element(StyleBodyCell).Text(index.ToString(CultureInfo.InvariantCulture));
        table.Cell().Element(StyleBodyCell).Text(text =>
        {
            var hyperlink = text.Hyperlink(issue.Key, issue.Url).Underline();
            _ = issue.Highlight
                ? hyperlink.FontColor(Colors.Orange.Darken2).SemiBold()
                : hyperlink.FontColor(Colors.Blue.Darken2);
        });

        foreach (var value in values)
        {
            var textColor = string.Equals(value, MULTI_ENTRY_ALERT_TEXT, StringComparison.Ordinal)
                ? Colors.Orange.Darken2
                : Colors.Black;
            var isAlert = string.Equals(value, MULTI_ENTRY_ALERT_TEXT, StringComparison.Ordinal);
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

    private const string MULTI_ENTRY_ALERT_TEXT = "MULTI-ENTRY";
    private readonly QaQueueReportDocumentBuilder _documentBuilder;
}
