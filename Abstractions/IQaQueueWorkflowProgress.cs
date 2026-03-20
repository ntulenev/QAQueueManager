using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Represents progress reporting for the QA queue workflow.
/// </summary>
internal interface IQaQueueWorkflowProgress
{
    /// <summary>
    /// Gets the build progress sink passed to the report service.
    /// </summary>
    IProgress<QaQueueBuildProgress> BuildProgress { get; }

    /// <summary>
    /// Marks the start of PDF export.
    /// </summary>
    void StartPdfExport();

    /// <summary>
    /// Marks PDF rendering as completed.
    /// </summary>
    void ReportPdfRendered();

    /// <summary>
    /// Marks PDF export as completed.
    /// </summary>
    /// <param name="path">The saved PDF path.</param>
    void ReportPdfSaved(ReportFilePath path);

    /// <summary>
    /// Marks the start of Excel export.
    /// </summary>
    void StartExcelExport();

    /// <summary>
    /// Marks Excel rendering as completed.
    /// </summary>
    void ReportExcelRendered();

    /// <summary>
    /// Marks Excel export as completed.
    /// </summary>
    /// <param name="path">The saved Excel path.</param>
    void ReportExcelSaved(ReportFilePath path);
}
