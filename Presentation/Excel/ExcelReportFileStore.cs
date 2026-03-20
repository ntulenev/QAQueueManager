using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Saves generated Excel workbooks to disk.
/// </summary>
internal sealed class ExcelReportFileStore : IExcelReportFileStore
{
    /// <summary>
    /// Saves the workbook stream to disk and returns the final path.
    /// </summary>
    /// <param name="contentStream">The workbook stream to save.</param>
    /// <param name="suggestedPath">The configured output path.</param>
    /// <returns>The final saved path.</returns>
    public ReportFilePath Save(Stream contentStream, ReportFilePath suggestedPath)
    {
        ArgumentNullException.ThrowIfNull(contentStream);

        var resolvedPath = ResolveOutputPath(suggestedPath);
        var directory = Path.GetDirectoryName(resolvedPath.Value);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        contentStream.Position = 0;
        using var fileStream = File.Create(resolvedPath.Value);
        contentStream.CopyTo(fileStream);
        return resolvedPath;
    }

    private static ReportFilePath ResolveOutputPath(ReportFilePath suggestedPath)
    {
        var extension = Path.GetExtension(suggestedPath.Value);
        var normalizedPath = string.IsNullOrWhiteSpace(extension) ? suggestedPath.Value + ".xlsx" : suggestedPath.Value;
        var absolutePath = Path.IsPathRooted(normalizedPath)
            ? normalizedPath
            : Path.Combine(Environment.CurrentDirectory, normalizedPath);

        var directory = Path.GetDirectoryName(absolutePath) ?? Environment.CurrentDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var finalExtension = Path.GetExtension(absolutePath);
        var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        return new ReportFilePath(Path.Combine(directory, $"{fileNameWithoutExtension}_{suffix}{finalExtension}"));
    }
}
