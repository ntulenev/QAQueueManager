using System.Globalization;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Telemetry;
using QAQueueManager.Presentation.Shared;

using Spectre.Console;

namespace QAQueueManager.Presentation;

/// <summary>
/// Renders the QA queue report to the interactive console using Spectre.Console.
/// </summary>
internal sealed class SpectreQaQueuePresentationService : IQaQueuePresentationService
{
    public SpectreQaQueuePresentationService(QaQueueReportDocumentBuilder documentBuilder)
    {
        ArgumentNullException.ThrowIfNull(documentBuilder);

        _documentBuilder = documentBuilder;
    }

    /// <inheritdoc />
    public void Render(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var document = _documentBuilder.Build(report);
        var header = document.Header;

        AnsiConsole.Write(new Rule($"[bold yellow]{Escape(header.Title)}[/]"));
        AnsiConsole.MarkupLine($"[grey]Generated:[/] {Escape(header.GeneratedAt)}");
        AnsiConsole.MarkupLine($"[grey]Target branch:[/] {Escape(header.TargetBranch)}");
        AnsiConsole.MarkupLine($"[grey]JQL:[/] {Escape(header.Jql)}");
        if (document.IsGroupedByTeam)
        {
            AnsiConsole.MarkupLine($"[grey]Grouping:[/] by team field {Escape(header.TeamGroupingField)}");
            AnsiConsole.MarkupLine($"[grey]Teams:[/] {header.TeamCount.ToString(CultureInfo.InvariantCulture)}");
        }

        AnsiConsole.MarkupLine(
            $"[grey]Totals:[/] no-code={header.NoCodeIssueCount}, repos={header.RepositoryCount}, hide-no-code={document.HideNoCodeIssues}");
        AnsiConsole.WriteLine();

        if (document.IsGroupedByTeam)
        {
            RenderTeamSections(document);
            return;
        }

        if (!document.HideNoCodeIssues)
        {
            RenderNoCodeSection("QA tasks without code", document.NoCodeIssues);
        }

        foreach (var repository in document.Repositories)
        {
            RenderRepositorySection(repository);
        }
    }

    /// <inheritdoc />
    public void RenderExportPaths(ReportFilePath pdfPath, ReportFilePath excelPath)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]PDF exported to:[/] {Escape(pdfPath.Value)}");
        AnsiConsole.MarkupLine($"[grey]Excel exported to:[/] {Escape(excelPath.Value)}");
    }

    /// <inheritdoc />
    public void RenderExecutionSummary(TimeSpan totalDuration, HttpRequestTelemetrySummary telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]HTTP telemetry[/]"));
        AnsiConsole.MarkupLine($"[grey]Elapsed:[/] {Escape(QaQueuePresentationFormatting.FormatDuration(totalDuration))}");
        AnsiConsole.MarkupLine($"[grey]Requests:[/] {telemetry.RequestCount.ToString(CultureInfo.InvariantCulture)}");
        AnsiConsole.MarkupLine($"[grey]Retries:[/] {telemetry.RetryCount.ToString(CultureInfo.InvariantCulture)}");
        AnsiConsole.MarkupLine($"[grey]Downloaded:[/] {Escape(QaQueuePresentationFormatting.FormatBytes(telemetry.ResponseBytes))}");
        AnsiConsole.MarkupLine($"[grey]HTTP time:[/] {Escape(QaQueuePresentationFormatting.FormatDuration(telemetry.TotalDuration))}");

        if (telemetry.Endpoints.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No HTTP requests were recorded.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        _ = table.AddColumn("Source");
        _ = table.AddColumn("Method");
        _ = table.AddColumn("Endpoint");
        _ = table.AddColumn("Count");
        _ = table.AddColumn("Retries");
        _ = table.AddColumn("Downloaded");
        _ = table.AddColumn("Total time");
        _ = table.AddColumn("Max");

        foreach (var endpoint in telemetry.Endpoints)
        {
            _ = table.AddRow(
                Escape(endpoint.Source),
                Escape(endpoint.Method),
                Escape(endpoint.Endpoint),
                endpoint.RequestCount.ToString(CultureInfo.InvariantCulture),
                endpoint.RetryCount.ToString(CultureInfo.InvariantCulture),
                Escape(QaQueuePresentationFormatting.FormatBytes(endpoint.ResponseBytes)),
                Escape(QaQueuePresentationFormatting.FormatDuration(endpoint.TotalDuration)),
                Escape(QaQueuePresentationFormatting.FormatDuration(endpoint.MaxDuration)));
        }

        AnsiConsole.Write(table);
    }

    /// <inheritdoc />
    public void RenderExcelMarkupSummary(ExcelMarkupMergeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Excel markup merge[/]"));
        AnsiConsole.MarkupLine($"[grey]Old reports folder:[/] {Escape(summary.OldReportsDirectoryPath)}");
        AnsiConsole.MarkupLine($"[grey]Source workbook:[/] {Escape(summary.PreviousReportPath)}");
        AnsiConsole.MarkupLine($"[grey]Merged rows:[/] {summary.MergedRowKeys.Count.ToString(CultureInfo.InvariantCulture)}");

        if (summary.MergedRowKeys.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No matching rows received restored markup.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        _ = table.AddColumn("Merged row key");

        foreach (var rowKey in summary.MergedRowKeys)
        {
            _ = table.AddRow(Escape(rowKey));
        }

        AnsiConsole.Write(table);
    }

    private static void RenderTeamSections(QaQueuePresentationDocument document)
    {
        foreach (var team in document.Teams)
        {
            AnsiConsole.Write(new Rule($"[bold yellow]Team: {Escape(team.TeamName)}[/]"));

            if (!document.HideNoCodeIssues)
            {
                RenderNoCodeSection("QA tasks without code", team.NoCodeIssues);
            }

            foreach (var repository in team.Repositories)
            {
                RenderRepositorySection(repository);
            }
        }
    }

    private static void RenderNoCodeSection(string title, IReadOnlyList<QaQueuePresentationNoCodeIssueRow> issues)
    {
        AnsiConsole.Write(new Rule($"[bold]{Escape(title)}[/]"));

        if (issues.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No QA tasks without code.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Expand();
        _ = table.AddColumn("#");
        _ = table.AddColumn("Issue");
        _ = table.AddColumn("Status");
        _ = table.AddColumn("Last updated");
        _ = table.AddColumn("Summary");

        foreach (var issue in issues)
        {
            _ = table.AddRow(
                issue.Index.ToString(CultureInfo.InvariantCulture),
                RenderIssueKey(issue.Issue),
                Escape(issue.Status),
                Escape(issue.LastUpdated),
                Escape(issue.Summary));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderRepositorySection(QaQueuePresentationRepositorySection repository)
    {
        AnsiConsole.Write(new Rule($"[bold]{Escape(repository.RepositoryName)}[/]"));

        if (repository.WithoutTargetMerge.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Tasks without merge into target branch[/]");

            var table = new Table().Border(TableBorder.Rounded).Expand();
            _ = table.AddColumn("#");
            _ = table.AddColumn("Issue");
            _ = table.AddColumn("Status");
            _ = table.AddColumn("PRs");
            _ = table.AddColumn("Branches");
            _ = table.AddColumn("Alert");
            _ = table.AddColumn("Last updated");
            _ = table.AddColumn("Summary");

            foreach (var item in repository.WithoutTargetMerge)
            {
                _ = table.AddRow(
                    item.Index.ToString(CultureInfo.InvariantCulture),
                    RenderIssueKey(item.Issue),
                    Escape(item.Status),
                    Escape(item.PullRequests),
                    Escape(item.Branches),
                    RenderAlertCell(item.Alert),
                    Escape(item.LastUpdated),
                    Escape(item.Summary));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        if (repository.MergedIssueRows.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[green]Tasks merged into target branch[/]");
        var mergedTable = new Table().Border(TableBorder.Rounded).Expand();
        _ = mergedTable.AddColumn("#");
        _ = mergedTable.AddColumn("Issue");
        _ = mergedTable.AddColumn("Status");
        _ = mergedTable.AddColumn("PRs");
        _ = mergedTable.AddColumn("Artifact version");
        _ = mergedTable.AddColumn("Alert");
        _ = mergedTable.AddColumn("Source");
        _ = mergedTable.AddColumn("Target");
        _ = mergedTable.AddColumn("Last updated");
        _ = mergedTable.AddColumn("Summary");

        foreach (var item in repository.MergedIssueRows)
        {
            _ = mergedTable.AddRow(
                item.Index.ToString(CultureInfo.InvariantCulture),
                RenderIssueKey(item.Issue),
                Escape(item.Status),
                Escape(item.PullRequests),
                Escape(item.ArtifactVersion),
                RenderAlertCell(item.Alert),
                Escape(item.Source),
                Escape(item.Target),
                Escape(item.LastUpdated),
                Escape(item.Summary));
        }

        AnsiConsole.Write(mergedTable);
        AnsiConsole.WriteLine();
    }

    private static string RenderIssueKey(QaQueuePresentationIssueRef issue) =>
        issue.Highlight
            ? $"[bold yellow]{Escape(issue.Key)}[/]"
            : Escape(issue.Key);

    private static string RenderAlertCell(string alert) =>
        string.Equals(alert, "MULTI-ENTRY", StringComparison.Ordinal)
            ? "[bold yellow]MULTI-ENTRY[/]"
            : Escape(alert);

    private static string Escape(string? value) => Markup.Escape(string.IsNullOrWhiteSpace(value) ? "-" : value);

    private readonly QaQueueReportDocumentBuilder _documentBuilder;
}
