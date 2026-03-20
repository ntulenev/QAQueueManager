using QAQueueManager.Abstractions;

namespace QAQueueManager.Presentation.Pdf;

internal sealed class PdfReportFileStore : IPdfReportFileStore
{
    public string Save(byte[] content, string suggestedPath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedPath);

        var resolvedPath = ResolveOutputPath(suggestedPath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(resolvedPath, content);
        return resolvedPath;
    }

    private static string ResolveOutputPath(string suggestedPath)
    {
        var extension = Path.GetExtension(suggestedPath);
        var normalizedPath = string.IsNullOrWhiteSpace(extension) ? suggestedPath + ".pdf" : suggestedPath;
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
