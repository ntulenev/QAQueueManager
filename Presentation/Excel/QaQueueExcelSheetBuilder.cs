using System.Globalization;

using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;
using QAQueueManager.Presentation.Shared;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Builds a single Excel worksheet for one report team section.
/// </summary>
internal sealed class QaQueueExcelSheetBuilder
{
    private const int SHEET_COLUMN_COUNT = 13;
    private const int MARKUP_KEY_COLUMN_INDEX = 13;
    private const string MARKUP_KEY_COLUMN_NAME = "MarkupKey";

    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueExcelSheetBuilder"/> class.
    /// </summary>
    /// <param name="jiraBrowseBaseUrl">The Jira browse base URL used for issue hyperlinks.</param>
    internal QaQueueExcelSheetBuilder(Uri jiraBrowseBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(jiraBrowseBaseUrl);

        _jiraBrowseBaseUrl = jiraBrowseBaseUrl;
    }

    /// <summary>
    /// Builds a worksheet for the supplied team section and report metadata.
    /// </summary>
    /// <param name="report">The source report.</param>
    /// <param name="team">The team section to render.</param>
    /// <param name="usedSheetNames">The set of already allocated sheet names.</param>
    /// <returns>The composed worksheet rows and layout metadata.</returns>
    internal QaQueueExcelBuiltSheet Build(
        QaQueueReport report,
        QaTeamSection team,
        HashSet<string> usedSheetNames)
    {
        var rows = new List<Dictionary<string, object?>>();
        var layout = CreateLayout(team.Team.Value, usedSheetNames);

        AddRow(rows, layout, ExcelCellStyleKind.Title, "A", $"{report.Title} | Team: {team.Team.Value}");
        AddLabeledValueRow(rows, layout, "Generated", QaQueuePresentationFormatting.FormatReportTimestamp(report.GeneratedAt));
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

        return new QaQueueExcelBuiltSheet(layout.Name, rows, layout);
    }

    private static ExcelSheetLayout CreateLayout(string teamName, HashSet<string> usedSheetNames)
    {
        var layout = new ExcelSheetLayout(BuildUniqueSheetName(teamName, usedSheetNames))
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
                [13] = 24,
            },
        };
        _ = layout.HiddenColumns.Add(MARKUP_KEY_COLUMN_INDEX);
        return layout;
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
        rows.Add(CreateGridRow("#", "Issue", "Status", "Assignee", "Last updated", "Summary", "Comment", MARKUP_KEY_COLUMN_NAME));
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
                QaQueuePresentationFormatting.FormatIssueTimestamp(issue.UpdatedAt),
                issue.Summary,
                string.Empty,
                QaQueueExcelMarkupKey.CreateNoCode(layout.Name, issue.Key).Value));
            layout.Hyperlinks[ToCellReference(2, currentRow)] = QaQueuePresentationFormatting.BuildIssueUrl(_jiraBrowseBaseUrl, issue.Key);
            layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;
        }

        layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 8, dataStartRow, rows.Count));
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
            rows.Add(CreateGridRow("#", "Issue", "Status", "Assignee", "PRs", "Branches", "Alert", "Last updated", "Summary", "Comment", MARKUP_KEY_COLUMN_NAME));
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
                    QaQueuePresentationFormatting.FormatPullRequests(item.PullRequests),
                    QaQueuePresentationFormatting.FormatBranchNames(item.BranchNames),
                    QaQueuePresentationFormatting.FormatAlertText(item.HasDuplicateIssue),
                    QaQueuePresentationFormatting.FormatIssueTimestamp(item.Issue.UpdatedAt),
                    item.Issue.Summary,
                    string.Empty,
                    QaQueueExcelMarkupKey.CreateWithoutMerge(layout.Name, repository.RepositoryFullName, item.Issue.Key).Value));
                layout.Hyperlinks[ToCellReference(2, currentRow)] = QaQueuePresentationFormatting.BuildIssueUrl(_jiraBrowseBaseUrl, item.Issue.Key);
                layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;

                if (item.PullRequests.Count == 1 && item.PullRequests[0].Url is not null)
                {
                    layout.Hyperlinks[ToCellReference(5, currentRow)] = item.PullRequests[0].Url!.ToString();
                    layout.CellStyles[ToCellReference(5, currentRow)] = ExcelCellStyleKind.Hyperlink;
                }

                if (item.HasDuplicateIssue)
                {
                    layout.CellStyles[ToCellReference(7, currentRow)] = ExcelCellStyleKind.Warning;
                }
            }

            layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 11, dataStartRow, rows.Count));
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
                "Comment",
                MARKUP_KEY_COLUMN_NAME));
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
                    QaQueuePresentationFormatting.FormatMergedPullRequests(item.PullRequests),
                    item.Version.Value,
                    QaQueuePresentationFormatting.FormatAlertText(item.HasDuplicateIssue),
                    QaQueuePresentationFormatting.FormatBranchNames(item.PullRequests.Select(static pr => pr.SourceBranch)),
                    QaQueuePresentationFormatting.FormatBranchNames(item.PullRequests.Select(static pr => pr.DestinationBranch)),
                    QaQueuePresentationFormatting.FormatIssueTimestamp(item.Issue.UpdatedAt),
                    item.Issue.Summary,
                    string.Empty,
                    QaQueueExcelMarkupKey.CreateMerged(layout.Name, repository.RepositoryFullName, item.Issue.Key, item.Version).Value));
                layout.Hyperlinks[ToCellReference(2, currentRow)] = QaQueuePresentationFormatting.BuildIssueUrl(_jiraBrowseBaseUrl, item.Issue.Key);
                layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;

                if (item.PullRequests.Count == 1 && item.PullRequests[0].PullRequestUrl is not null)
                {
                    layout.Hyperlinks[ToCellReference(5, currentRow)] = item.PullRequests[0].PullRequestUrl!.ToString();
                    layout.CellStyles[ToCellReference(5, currentRow)] = ExcelCellStyleKind.Hyperlink;
                }

                if (item.HasDuplicateIssue)
                {
                    layout.CellStyles[ToCellReference(7, currentRow)] = ExcelCellStyleKind.Warning;
                }
            }

            layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 13, dataStartRow, rows.Count));
            AddBlankRow(rows);
        }
    }

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

    private readonly Uri _jiraBrowseBaseUrl;
}
