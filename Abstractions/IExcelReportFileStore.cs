namespace QAQueueManager.Abstractions;

/// <summary>
/// Persists rendered Excel workbooks to disk.
/// </summary>
internal interface IExcelReportFileStore
{
    /// <summary>
    /// Saves an Excel workbook stream to the configured output location.
    /// </summary>
    /// <param name="contentStream">The workbook stream to save.</param>
    /// <param name="suggestedPath">The configured output path.</param>
    /// <returns>The final path of the saved workbook.</returns>
    string Save(Stream contentStream, string suggestedPath);
}
