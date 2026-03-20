using System.Diagnostics;

using QAQueueManager.Abstractions;

namespace QAQueueManager.Presentation.Pdf;

/// <summary>
/// Opens generated PDF files using the operating system shell.
/// </summary>
internal sealed class PdfReportLauncher : IPdfReportLauncher
{
    /// <summary>
    /// Opens the specified PDF path.
    /// </summary>
    /// <param name="path">The PDF path to open.</param>
    public void Launch(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        _ = Process.Start(startInfo);
    }
}
