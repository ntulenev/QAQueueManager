using System.Globalization;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Composes workbook sheets and layout metadata for Excel export.
/// </summary>
internal sealed class QaQueueExcelContentComposer : IExcelWorkbookContentComposer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueExcelContentComposer"/> class.
    /// </summary>
    /// <param name="jiraOptions">The Jira configuration options used to build issue links.</param>
    public QaQueueExcelContentComposer(IOptions<JiraOptions> jiraOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraOptions);

        _jiraBrowseBaseUrl = new Uri(jiraOptions.Value.BaseUrl.ToString().TrimEnd('/') + "/browse/", UriKind.Absolute);
    }

    /// <summary>
    /// Converts the domain report into workbook sheet data and layout metadata.
    /// </summary>
    /// <param name="report">The report to convert.</param>
    /// <returns>The composed workbook data.</returns>
    public ExcelWorkbookData ComposeWorkbook(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sheets = new Dictionary<ExcelSheetName, object>();
        var layouts = new Dictionary<ExcelSheetName, ExcelSheetLayout>();
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var teams = report.IsGroupedByTeam
            ? report.Teams
            : [new QaTeamSection(new TeamName("All Teams"), report.NoCodeIssues, report.Repositories)];

        foreach (var team in teams)
        {
            var builtSheet = BuildTeamSheet(report, team, usedSheetNames);
            sheets.Add(builtSheet.Name, builtSheet.Rows);
            layouts.Add(builtSheet.Name, builtSheet.Layout);
        }

        return new ExcelWorkbookData(sheets, layouts);
    }

    private BuiltSheet BuildTeamSheet(
        QaQueueReport report,
        QaTeamSection team,
        HashSet<string> usedSheetNames)
    {
        var rows = new List<Dictionary<string, object?>>();
        var layout = new ExcelSheetLayout(BuildUniqueSheetName(team.Team.Value, usedSheetNames))
        {
            ColumnWidths =
            {
                [1] = 6,
                [2] = 16,
                [3] = 18,
                [4] = 18,
                [5] = 18,
                [6] = 18,
                [7] = 18,
                [8] = 16,
                [9] = 16,
                [10] = 18,
                [11] = 48,
                [12] = 24,
            },
        };

        AddRow(rows, layout, ExcelCellStyleKind.Title, "A", $"{report.Title} | Team: {team.Team.Value}");
        AddLabeledValueRow(rows, layout, "Generated", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        AddLabeledValueRow(rows, layout, "Target branch", report.TargetBranch.Value);
        AddLabeledValueRow(rows, layout, "JQL", report.Jql);
        AddBlankRow(rows);

        if (!report.HideNoCodeIssues)
        {
            AppendNoCodeSection(rows, layout, team.NoCodeIssues);
        }

        foreach (var repository in team.Repositories)
        {
            AppendRepositorySection(rows, layout, repository);
        }

        return new BuiltSheet(layout.Name, rows, layout);
    }

    private void AppendNoCodeSection(
        List<Dictionary<string, object?>> rows,
        ExcelSheetLayout layout,
        IReadOnlyList<QaIssue> issues)
    {
        AddRow(rows, layout, ExcelCellStyleKind.SectionTitle, "A", "QA tasks without code");
        if (issues.Count == 0)
        {
            AddRow(rows, layout, ExcelCellStyleKind.Muted, "A", "No QA tasks without code.");
            AddBlankRow(rows);
            return;
        }

        var headerRow = rows.Count + 1;
        rows.Add(CreateGridRow("#", "Issue", "Status", "Assignee", "Last updated", "Summary", "Comment"));
        var dataStartRow = rows.Count + 1;

        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index];
            var currentRow = rows.Count + 1;
            rows.Add(CreateGridRow(
                index + 1,
                issue.Key.Value,
                issue.Status.Value,
                issue.Assignee,
                FormatDate(issue.UpdatedAt),
                issue.Summary,
                string.Empty));
            layout.Hyperlinks[ToCellReference(2, currentRow)] = BuildIssueUrl(issue.Key);
            layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;
        }

        layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 7, dataStartRow, rows.Count));
        AddBlankRow(rows);
    }

    private void AppendRepositorySection(
        List<Dictionary<string, object?>> rows,
        ExcelSheetLayout layout,
        QaRepositorySection repository)
    {
        AddRow(rows, layout, ExcelCellStyleKind.SectionTitle, "A", repository.RepositoryFullName.Value);

        if (repository.WithoutTargetMerge.Count > 0)
        {
            AddRow(rows, layout, ExcelCellStyleKind.MetadataLabel, "A", "Tasks without merge into target branch");
            var headerRow = rows.Count + 1;
            rows.Add(CreateGridRow("#", "Issue", "Status", "Assignee", "PRs", "Branches", "Last updated", "Summary", "Comment"));
            var dataStartRow = rows.Count + 1;

            for (var index = 0; index < repository.WithoutTargetMerge.Count; index++)
            {
                var item = repository.WithoutTargetMerge[index];
                var currentRow = rows.Count + 1;
                rows.Add(CreateGridRow(
                    index + 1,
                    item.Issue.Key.Value,
                    item.Issue.Status.Value,
                    item.Issue.Assignee,
                    FormatPullRequests(item.PullRequests),
                    FormatBranchNames(item.BranchNames),
                    FormatDate(item.Issue.UpdatedAt),
                    item.Issue.Summary,
                    string.Empty));
                layout.Hyperlinks[ToCellReference(2, currentRow)] = BuildIssueUrl(item.Issue.Key);
                layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;

                if (item.PullRequests.Count == 1 && item.PullRequests[0].Url is not null)
                {
                    layout.Hyperlinks[ToCellReference(5, currentRow)] = item.PullRequests[0].Url!.ToString();
                    layout.CellStyles[ToCellReference(5, currentRow)] = ExcelCellStyleKind.Hyperlink;
                }
            }

            layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 9, dataStartRow, rows.Count));
            AddBlankRow(rows);
        }

        if (repository.MergedIssueRows.Count > 0)
        {
            AddRow(rows, layout, ExcelCellStyleKind.MetadataLabel, "A", "Tasks merged into target branch");
            var headerRow = rows.Count + 1;
            rows.Add(CreateGridRow(
                "#",
                "Issue",
                "Status",
                "Assignee",
                "PRs",
                "Artifact version",
                "Alert",
                "Source",
                "Target",
                "Last updated",
                "Summary",
                "Comment"));
            var dataStartRow = rows.Count + 1;

            for (var index = 0; index < repository.MergedIssueRows.Count; index++)
            {
                var item = repository.MergedIssueRows[index];
                var currentRow = rows.Count + 1;
                rows.Add(CreateGridRow(
                    index + 1,
                    item.Issue.Key.Value,
                    item.Issue.Status.Value,
                    item.Issue.Assignee,
                    FormatMergedPullRequests(item.PullRequests),
                    item.Version.Value,
                    item.HasMultipleVersions ? MULTI_VERSION_ALERT_TEXT : "-",
                    FormatBranchNames(item.PullRequests.Select(static pr => pr.SourceBranch)),
                    FormatBranchNames(item.PullRequests.Select(static pr => pr.DestinationBranch)),
                    FormatDate(item.Issue.UpdatedAt),
                    item.Issue.Summary,
                    string.Empty));
                layout.Hyperlinks[ToCellReference(2, currentRow)] = BuildIssueUrl(item.Issue.Key);
                layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;

                if (item.PullRequests.Count == 1 && item.PullRequests[0].PullRequestUrl is not null)
                {
                    layout.Hyperlinks[ToCellReference(5, currentRow)] = item.PullRequests[0].PullRequestUrl!.ToString();
                    layout.CellStyles[ToCellReference(5, currentRow)] = ExcelCellStyleKind.Hyperlink;
                }

                if (item.HasMultipleVersions)
                {
                    layout.CellStyles[ToCellReference(7, currentRow)] = ExcelCellStyleKind.Warning;
                }
            }

            layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 12, dataStartRow, rows.Count));
            AddBlankRow(rows);
        }
    }

    private string BuildIssueUrl(JiraIssueKey issueKey) =>
        new Uri(_jiraBrowseBaseUrl, Uri.EscapeDataString(issueKey.Value)).ToString();

    private static Dictionary<string, object?> CreateGridRow(params object?[] values)
    {
        var row = new Dictionary<string, object?>(SHEET_COLUMN_COUNT, StringComparer.Ordinal);
        for (var columnIndex = 1; columnIndex <= SHEET_COLUMN_COUNT; columnIndex++)
        {
            row.Add("C" + columnIndex.ToString(CultureInfo.InvariantCulture), columnIndex <= values.Length ? values[columnIndex - 1] ?? string.Empty : string.Empty);
        }

        return row;
    }

    private static void AddBlankRow(List<Dictionary<string, object?>> rows) =>
        rows.Add(CreateGridRow(string.Empty));

    private static void AddRow(
        List<Dictionary<string, object?>> rows,
        ExcelSheetLayout layout,
        ExcelCellStyleKind styleKind,
        string columnName,
        string value)
    {
        var row = CreateGridRow(string.Empty);
        var columnIndex = ColumnNameToIndex(columnName);
        row["C" + columnIndex.ToString(CultureInfo.InvariantCulture)] = value;
        rows.Add(row);
        layout.CellStyles[columnName + rows.Count.ToString(CultureInfo.InvariantCulture)] = styleKind;
    }

    private static void AddLabeledValueRow(
        List<Dictionary<string, object?>> rows,
        ExcelSheetLayout layout,
        string label,
        string value)
    {
        rows.Add(CreateGridRow(label, value));
        var rowIndex = rows.Count;
        layout.CellStyles["A" + rowIndex.ToString(CultureInfo.InvariantCulture)] = ExcelCellStyleKind.MetadataLabel;
    }

    private static ExcelSheetName BuildUniqueSheetName(string baseName, HashSet<string> usedNames)
    {
        var sanitized = ExcelSheetName.Sanitize(baseName, "Team");
        var candidate = sanitized.Value;
        var suffix = 2;

        while (!usedNames.Add(candidate))
        {
            var suffixValue = "_" + suffix.ToString(CultureInfo.InvariantCulture);
            var maxBaseLength = 31 - suffixValue.Length;
            candidate = sanitized.Value[..Math.Min(sanitized.Value.Length, maxBaseLength)] + suffixValue;
            suffix++;
        }

        return new ExcelSheetName(candidate);
    }

    private static int ColumnNameToIndex(string columnName)
    {
        var result = 0;
        foreach (var symbol in columnName)
        {
            var letterValue = char.ToUpperInvariant(symbol) - 'A' + 1;
            result = (result * 26) + letterValue;
        }

        return result;
    }

    private static string ToCellReference(int columnIndex, int rowIndex) =>
        ToColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);

    private static string ToColumnName(int columnIndex)
    {
        var dividend = columnIndex;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo, CultureInfo.InvariantCulture) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string FormatDate(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";

    private static string FormatPullRequests(IReadOnlyList<JiraPullRequestLink> pullRequests)
    {
        return pullRequests.Count == 0
            ? "-"
            : string.Join(", ", pullRequests.Select(static pr => $"#{pr.Id}:{pr.Status.Value}->{pr.DestinationBranch.Value}"));
    }

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

    private readonly Uri _jiraBrowseBaseUrl;

    /// <summary>
    /// Represents a fully composed worksheet and its layout metadata.
    /// </summary>
    /// <param name="Name">The worksheet name.</param>
    /// <param name="Rows">The worksheet rows.</param>
    /// <param name="Layout">The worksheet layout metadata.</param>
    private sealed record BuiltSheet(
        ExcelSheetName Name,
        List<Dictionary<string, object?>> Rows,
        ExcelSheetLayout Layout);

    private const int SHEET_COLUMN_COUNT = 12;
    private const string MULTI_VERSION_ALERT_TEXT = "MULTI-VERSION";
}
