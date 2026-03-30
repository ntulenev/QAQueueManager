namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents an in-memory Excel workbook together with formatter metadata.
/// </summary>
/// <param name="WorkbookStream">The generated workbook stream.</param>
/// <param name="MarkupMergeSummary">The summary of restored manual markup.</param>
internal sealed record ExcelRenderResult(
    MemoryStream WorkbookStream,
    ExcelMarkupMergeSummary MarkupMergeSummary);
