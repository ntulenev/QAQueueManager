namespace QAQueueManager.Abstractions;

/// <summary>
/// Persists rendered PDF reports to disk.
/// </summary>
internal interface IPdfReportFileStore
{
    /// <summary>
    /// Saves PDF content to the configured output location.
    /// </summary>
    /// <param name="content">The PDF bytes to save.</param>
    /// <param name="suggestedPath">The configured output path.</param>
    /// <returns>The final path of the saved PDF file.</returns>
    string Save(byte[] content, string suggestedPath);
}
