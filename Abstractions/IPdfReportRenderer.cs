using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Renders the QA report into PDF bytes.
/// </summary>
internal interface IPdfReportRenderer
{
    /// <summary>
    /// Renders the supplied report to PDF.
    /// </summary>
    /// <param name="report">The report to render.</param>
    /// <returns>The generated PDF bytes.</returns>
    byte[] Render(QaQueueReport report);
}
