using System.Globalization;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation.Pdf;

/// <summary>
/// Saves generated PDF reports to disk.
/// </summary>
internal sealed class PdfReportFileStore : IPdfReportFileStore
{
    /// <summary>
    /// Saves PDF content to disk and returns the final path.
    /// </summary>
    /// <param name="content">The PDF bytes to save.</param>
    /// <param name="suggestedPath">The configured output path.</param>
    /// <returns>The final saved path.</returns>
    public ReportFilePath Save(byte[] content, ReportFilePath suggestedPath)
    {
        ArgumentNullException.ThrowIfNull(content);

        var resolvedPath = ResolveOutputPath(suggestedPath);
        var directory = Path.GetDirectoryName(resolvedPath.Value);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(resolvedPath.Value, content);
        return resolvedPath;
    }

    private static ReportFilePath ResolveOutputPath(ReportFilePath suggestedPath)
    {
        var extension = Path.GetExtension(suggestedPath.Value);
        var normalizedPath = string.IsNullOrWhiteSpace(extension) ? suggestedPath.Value + ".pdf" : suggestedPath.Value;
        var absolutePath = Path.IsPathRooted(normalizedPath)
            ? normalizedPath
            : Path.Combine(Environment.CurrentDirectory, normalizedPath);

        var directory = Path.GetDirectoryName(absolutePath) ?? Environment.CurrentDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var finalExtension = Path.GetExtension(absolutePath);
        var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        return new ReportFilePath(Path.Combine(directory, $"{fileNameWithoutExtension}_{suffix}{finalExtension}"));
    }
}
