using Spectre.Console;

using QAQueueManager.Abstractions;
using QAQueueManager.Logic;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation;

/// <summary>
/// Renders the QA queue report to the interactive console using Spectre.Console.
/// </summary>
internal sealed class SpectreQaQueuePresentationService : IQaQueuePresentationService
{
    /// <summary>
    /// Renders the supplied report to the console.
    /// </summary>
    /// <param name="report">The report to render.</param>
    public void Render(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var repositoryCount = report.IsGroupedByTeam
            ? report.Teams.Sum(static team => team.Repositories.Count)
            : report.Repositories.Count;

        AnsiConsole.Write(new Rule($"[bold yellow]{Escape(report.Title)}[/]"));
        AnsiConsole.MarkupLine($"[grey]Generated:[/] {report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        AnsiConsole.MarkupLine($"[grey]Target branch:[/] {Escape(report.TargetBranch)}");
        AnsiConsole.MarkupLine($"[grey]JQL:[/] {Escape(report.Jql)}");
        if (report.IsGroupedByTeam)
        {
            AnsiConsole.MarkupLine($"[grey]Grouping:[/] by team field {Escape(report.TeamGroupingField)}");
            AnsiConsole.MarkupLine($"[grey]Teams:[/] {report.Teams.Count}");
        }

        AnsiConsole.MarkupLine(
            $"[grey]Totals:[/] no-code={report.NoCodeIssues.Count}, repos={repositoryCount}, hide-no-code={report.HideNoCodeIssues}");
        AnsiConsole.WriteLine();

        if (report.IsGroupedByTeam)
        {
            RenderTeamSections(report);
            return;
        }

        if (!report.HideNoCodeIssues)
        {
            RenderNoCodeSection("QA tasks without code", report.NoCodeIssues);
        }

        foreach (var repository in report.Repositories)
        {
            RenderRepositorySection(repository);
        }
    }

    private static void RenderTeamSections(QaQueueReport report)
    {
        foreach (var team in report.Teams)
        {
            AnsiConsole.Write(new Rule($"[bold yellow]Team: {Escape(team.Team)}[/]"));

            if (!report.HideNoCodeIssues)
            {
                RenderNoCodeSection("QA tasks without code", team.NoCodeIssues);
            }

            foreach (var repository in team.Repositories)
            {
                RenderRepositorySection(repository);
            }
        }
    }

    private static void RenderNoCodeSection(string title, IReadOnlyList<QaIssue> issues)
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

        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index];
            _ = table.AddRow(
                (index + 1).ToString(),
                Escape(issue.Key),
                Escape(issue.Status),
                Escape(FormatDate(issue.UpdatedAt)),
                Escape(issue.Summary));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderRepositorySection(QaRepositorySection repository)
    {
        AnsiConsole.Write(new Rule($"[bold]{Escape(repository.RepositoryFullName)}[/]"));

        if (repository.WithoutTargetMerge.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Tasks without merge into target branch[/]");

            var table = new Table().Border(TableBorder.Rounded).Expand();
            _ = table.AddColumn("#");
            _ = table.AddColumn("Issue");
            _ = table.AddColumn("Status");
            _ = table.AddColumn("PRs");
            _ = table.AddColumn("Branches");
            _ = table.AddColumn("Last updated");
            _ = table.AddColumn("Summary");

            for (var index = 0; index < repository.WithoutTargetMerge.Count; index++)
            {
                var item = repository.WithoutTargetMerge[index];
                _ = table.AddRow(
                    (index + 1).ToString(),
                    Escape(item.Issue.Key),
                    Escape(item.Issue.Status),
                    Escape(FormatPullRequests(item.PullRequests)),
                    Escape(string.Join(", ", item.BranchNames)),
                    Escape(FormatDate(item.Issue.UpdatedAt)),
                    Escape(item.Issue.Summary));
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

        for (var index = 0; index < repository.MergedIssueRows.Count; index++)
        {
            var item = repository.MergedIssueRows[index];
            _ = mergedTable.AddRow(
                (index + 1).ToString(),
                FormatIssueCell(item),
                Escape(item.Issue.Status),
                Escape(FormatMergedPullRequests(item.PullRequests)),
                Escape(item.Version),
                FormatAlertCell(item),
                Escape(FormatBranchNames(item.PullRequests.Select(static pr => pr.SourceBranch))),
                Escape(FormatBranchNames(item.PullRequests.Select(static pr => pr.DestinationBranch))),
                Escape(FormatDate(item.Issue.UpdatedAt)),
                Escape(item.Issue.Summary));
        }

        AnsiConsole.Write(mergedTable);
        AnsiConsole.WriteLine();
    }

    private static string FormatPullRequests(IReadOnlyList<JiraPullRequestLink> pullRequests)
    {
        if (pullRequests.Count == 0)
        {
            return "-";
        }

        return string.Join(
            ", ",
            pullRequests.Select(static pr => $"#{pr.Id}:{pr.Status}->{pr.DestinationBranch}"));
    }

    private static string FormatMergedPullRequests(IReadOnlyList<QaMergedPullRequest> pullRequests)
    {
        if (pullRequests.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", pullRequests.Select(static pr => $"#{pr.PullRequestId}"));
    }

    private static string FormatBranchNames(IEnumerable<string> branchNames)
    {
        var values = branchNames
            .Where(static branch => !string.IsNullOrWhiteSpace(branch))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? "-" : string.Join(", ", values);
    }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    private static string FormatIssueCell(QaMergedIssueVersionRow item) =>
        item.HasMultipleVersions
            ? $"[bold yellow]{Escape(item.Issue.Key)}[/]"
            : Escape(item.Issue.Key);

    private static string FormatAlertCell(QaMergedIssueVersionRow item) =>
        item.HasMultipleVersions
            ? "[bold yellow]MULTI-VERSION[/]"
            : Escape("-");

    private static string Escape(string? value) => Markup.Escape(string.IsNullOrWhiteSpace(value) ? "-" : value);
}
