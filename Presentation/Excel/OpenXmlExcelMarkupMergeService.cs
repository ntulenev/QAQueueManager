using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Restores manual markup from previous workbooks into the generated Excel workbook.
/// </summary>
internal sealed class OpenXmlExcelMarkupMergeService : IExcelMarkupMergeService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenXmlExcelMarkupMergeService"/> class.
    /// </summary>
    /// <param name="reportOptions">The report configuration options.</param>
    public OpenXmlExcelMarkupMergeService(IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(reportOptions);

        _historyLocator = new OpenXmlExcelReportHistoryLocator(reportOptions.Value.OldReportsPath);
    }

    /// <inheritdoc />
    public ExcelMarkupMergeSummary Merge(Stream workbookStream, IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(workbookStream);
        ArgumentNullException.ThrowIfNull(layouts);

        workbookStream.Position = 0;
        var resolvedOldReportsDirectoryPath = _historyLocator.ResolveOldReportsDirectoryPath();
        var previousReportPath = OpenXmlExcelReportHistoryLocator.ResolvePreviousReportPath(resolvedOldReportsDirectoryPath);
        var mergedRowKeys = new List<string>();
        using var previousWorkbook = OpenPreviousWorkbook(previousReportPath);
        using (var spreadsheet = SpreadsheetDocument.Open(workbookStream, true))
        {
            var workbookPart = spreadsheet.WorkbookPart
                ?? throw new InvalidOperationException("Workbook part is missing.");
            var workbook = workbookPart.Workbook
                ?? throw new InvalidOperationException("Workbook is missing.");
            var builtInFillSignatures = GetFillSignatures(workbookPart.WorkbookStylesPart?.Stylesheet);

            foreach (var sheet in workbook.Sheets?.OfType<Sheet>() ?? [])
            {
                if (!ExcelSheetName.TryCreate(sheet.Name?.Value, out var sheetName) ||
                    !layouts.TryGetValue(sheetName, out var layout))
                {
                    continue;
                }

                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                if (previousWorkbook is not null)
                {
                    mergedRowKeys.AddRange(OpenXmlExcelWorksheetMarkupRestorer.Restore(
                        previousWorkbook,
                        workbookPart,
                        worksheetPart,
                        layout.Name,
                        builtInFillSignatures));
                }
            }

            workbook.Save();
        }

        workbookStream.Position = 0;
        return new ExcelMarkupMergeSummary(
            resolvedOldReportsDirectoryPath,
            previousReportPath,
            [.. mergedRowKeys.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    private static HashSet<string> GetFillSignatures(Stylesheet? stylesheet)
    {
        if (stylesheet?.Fills is null)
        {
            return [];
        }

        return [.. stylesheet.Fills
            .Elements<Fill>()
            .Select(static fill => fill.OuterXml)
            .Where(static xml => !string.IsNullOrWhiteSpace(xml))];
    }

    private static SpreadsheetDocument? OpenPreviousWorkbook(string? previousReportPath)
    {
        return string.IsNullOrWhiteSpace(previousReportPath)
            ? null
            : SpreadsheetDocument.Open(previousReportPath, false);
    }
    private readonly OpenXmlExcelReportHistoryLocator _historyLocator;
}
