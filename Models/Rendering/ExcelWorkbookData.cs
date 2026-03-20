namespace QAQueueManager.Models.Rendering;

/// <summary>
/// Represents workbook content and layout metadata used for Excel export.
/// </summary>
/// <param name="Sheets">The workbook sheets keyed by sheet name.</param>
/// <param name="Layouts">The sheet layout metadata keyed by sheet name.</param>
internal sealed record ExcelWorkbookData(
    IReadOnlyDictionary<ExcelSheetName, object> Sheets,
    IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout> Layouts);
