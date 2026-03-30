using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Restores manual markup from a previous workbook into the generated Excel workbook.
/// </summary>
internal interface IExcelMarkupMergeService
{
    /// <summary>
    /// Merges legacy manual markup into the supplied workbook stream.
    /// </summary>
    /// <param name="workbookStream">The workbook stream to update.</param>
    /// <param name="layouts">Per-sheet layout metadata.</param>
    /// <returns>The summary of restored markup.</returns>
    ExcelMarkupMergeSummary Merge(Stream workbookStream, IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout> layouts);
}
