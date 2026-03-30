using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Applies OpenXML formatting to a generated workbook.
/// </summary>
internal interface IWorkbookFormatter
{
    /// <summary>
    /// Formats the workbook stream using the supplied sheet layouts.
    /// </summary>
    /// <param name="workbookStream">The workbook stream to format.</param>
    /// <param name="layouts">Per-sheet layout metadata.</param>
    ExcelMarkupMergeSummary Format(Stream workbookStream, IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout> layouts);
}
