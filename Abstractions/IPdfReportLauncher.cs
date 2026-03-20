namespace QAQueueManager.Abstractions;

/// <summary>
/// Opens generated PDF reports with the operating system shell.
/// </summary>
internal interface IPdfReportLauncher
{
    /// <summary>
    /// Launches the specified PDF file.
    /// </summary>
    /// <param name="path">The PDF path to open.</param>
    void Launch(string path);
}
