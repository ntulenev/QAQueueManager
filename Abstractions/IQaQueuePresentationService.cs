using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Telemetry;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Renders the QA report to the console.
/// </summary>
internal interface IQaQueuePresentationService
{
    /// <summary>
    /// Writes the supplied report to the interactive console output.
    /// </summary>
    /// <param name="report">The report to render.</param>
    void Render(QaQueueReport report);

    /// <summary>
    /// Writes the exported report locations to the interactive console output.
    /// </summary>
    /// <param name="pdfPath">The exported PDF path.</param>
    /// <param name="excelPath">The exported Excel path.</param>
    void RenderExportPaths(ReportFilePath pdfPath, ReportFilePath excelPath);

    /// <summary>
    /// Writes the execution telemetry summary to the interactive console output.
    /// </summary>
    /// <param name="totalDuration">The total application execution time.</param>
    /// <param name="telemetry">The aggregated HTTP telemetry.</param>
    void RenderExecutionSummary(TimeSpan totalDuration, HttpRequestTelemetrySummary telemetry);
}
