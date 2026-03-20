using System.Diagnostics;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

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
    public void Launch(ReportFilePath path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path.Value,
            UseShellExecute = true
        };

        _ = Process.Start(startInfo);
    }
}
