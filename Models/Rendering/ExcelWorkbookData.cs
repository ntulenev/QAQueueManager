namespace QAQueueManager.Models.Rendering;

internal sealed record ExcelWorkbookData(
    IReadOnlyDictionary<string, object> Sheets,
    IReadOnlyDictionary<string, ExcelSheetLayout> Layouts);
