using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Represents a fully composed worksheet and its layout metadata.
/// </summary>
/// <param name="Name">The worksheet name.</param>
/// <param name="Rows">The worksheet rows.</param>
/// <param name="Layout">The worksheet layout metadata.</param>
internal sealed record QaQueueExcelBuiltSheet(
    ExcelSheetName Name,
    List<Dictionary<string, object?>> Rows,
    ExcelSheetLayout Layout);
