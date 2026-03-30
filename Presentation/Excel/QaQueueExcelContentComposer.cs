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

        var jiraBrowseBaseUrl = new Uri(jiraOptions.Value.BaseUrl.ToString().TrimEnd('/') + "/browse/", UriKind.Absolute);
        _sheetBuilder = new QaQueueExcelSheetBuilder(jiraBrowseBaseUrl);
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
            var builtSheet = _sheetBuilder.Build(report, team, usedSheetNames);
            sheets.Add(builtSheet.Name, builtSheet.Rows);
            layouts.Add(builtSheet.Name, builtSheet.Layout);
        }

        return new ExcelWorkbookData(sheets, layouts);
    }

    private readonly QaQueueExcelSheetBuilder _sheetBuilder;
}
