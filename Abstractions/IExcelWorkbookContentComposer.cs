using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Builds sheet-oriented workbook content from the domain report model.
/// </summary>
internal interface IExcelWorkbookContentComposer
{
    /// <summary>
    /// Converts the domain report into workbook data and layout metadata.
    /// </summary>
    /// <param name="report">The report to convert.</param>
    /// <returns>The workbook content representation.</returns>
    ExcelWorkbookData ComposeWorkbook(QaQueueReport report);
}
