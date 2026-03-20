using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Renders the QA report into an Excel workbook stream.
/// </summary>
internal interface IExcelReportRenderer
{
    /// <summary>
    /// Renders the supplied report into an in-memory Excel workbook.
    /// </summary>
    /// <param name="report">The report to render.</param>
    /// <returns>A memory stream containing the generated workbook.</returns>
    MemoryStream Render(QaQueueReport report);
}
