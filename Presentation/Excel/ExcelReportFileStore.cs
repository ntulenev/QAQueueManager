using QAQueueManager.Abstractions;

namespace QAQueueManager.Presentation.Excel;

internal sealed class ExcelReportFileStore : IExcelReportFileStore
{
    public string Save(Stream contentStream, string suggestedPath)
    {
        ArgumentNullException.ThrowIfNull(contentStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedPath);

        var resolvedPath = ResolveOutputPath(suggestedPath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        contentStream.Position = 0;
        using var fileStream = File.Create(resolvedPath);
        contentStream.CopyTo(fileStream);
        return resolvedPath;
    }

    private static string ResolveOutputPath(string suggestedPath)
    {
        var extension = Path.GetExtension(suggestedPath);
        var normalizedPath = string.IsNullOrWhiteSpace(extension) ? suggestedPath + ".xlsx" : suggestedPath;
        var absolutePath = Path.IsPathRooted(normalizedPath)
            ? normalizedPath
            : Path.Combine(Environment.CurrentDirectory, normalizedPath);

        var directory = Path.GetDirectoryName(absolutePath) ?? Environment.CurrentDirectory;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var finalExtension = Path.GetExtension(absolutePath);
        var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        return Path.Combine(directory, $"{fileNameWithoutExtension}_{suffix}{finalExtension}");
    }
}
