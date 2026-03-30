namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Resolves old report locations used to restore Excel markup.
/// </summary>
internal sealed class OpenXmlExcelReportHistoryLocator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenXmlExcelReportHistoryLocator"/> class.
    /// </summary>
    /// <param name="oldReportsPath">The configured old reports path.</param>
    public OpenXmlExcelReportHistoryLocator(string? oldReportsPath) => _oldReportsPath = oldReportsPath;

    /// <summary>
    /// Resolves the configured old reports directory to an absolute path.
    /// </summary>
    /// <returns>The resolved absolute directory path, or <see langword="null"/> when not configured.</returns>
    internal string? ResolveOldReportsDirectoryPath()
    {
        if (string.IsNullOrWhiteSpace(_oldReportsPath))
        {
            return null;
        }

        return Path.IsPathRooted(_oldReportsPath)
            ? _oldReportsPath
            : Path.Combine(Environment.CurrentDirectory, _oldReportsPath);
    }

    /// <summary>
    /// Finds the newest workbook in the resolved old reports directory.
    /// </summary>
    /// <param name="resolvedDirectory">The resolved absolute reports directory.</param>
    /// <returns>The newest workbook path, or <see langword="null"/> when none exists.</returns>
    internal static string? ResolvePreviousReportPath(string? resolvedDirectory)
    {
        if (string.IsNullOrWhiteSpace(resolvedDirectory) || !Directory.Exists(resolvedDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(resolvedDirectory, "*.xlsx", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private readonly string? _oldReportsPath;
}
