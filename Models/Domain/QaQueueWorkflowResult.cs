namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents the result of a QA queue workflow run.
/// </summary>
/// <param name="Report">The generated QA queue report.</param>
/// <param name="PdfPath">The exported PDF path.</param>
/// <param name="ExcelPath">The exported Excel path.</param>
internal sealed record QaQueueWorkflowResult(
    QaQueueReport Report,
    ReportFilePath PdfPath,
    ReportFilePath ExcelPath);
