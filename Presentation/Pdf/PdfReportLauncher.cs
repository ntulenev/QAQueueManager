using System.Diagnostics;

using QAQueueManager.Abstractions;

namespace QAQueueManager.Presentation.Pdf;

internal sealed class PdfReportLauncher : IPdfReportLauncher
{
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
