using FluentAssertions;

using QAQueueManager.Presentation.Excel;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class OpenXmlExcelReportHistoryLocatorTests
{
    [Fact(DisplayName = "ResolveOldReportsDirectoryPath returns an absolute path unchanged")]
    [Trait("Category", "Unit")]
    public void ResolveOldReportsDirectoryPathReturnsAbsolutePathUnchanged()
    {
        var locator = new OpenXmlExcelReportHistoryLocator(@"C:\reports\old");

        locator.ResolveOldReportsDirectoryPath().Should().Be(@"C:\reports\old");
    }

    [Fact(DisplayName = "ResolveOldReportsDirectoryPath combines relative path with current directory")]
    [Trait("Category", "Unit")]
    public void ResolveOldReportsDirectoryPathCombinesRelativePath()
    {
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            Environment.CurrentDirectory = tempDirectory;
            var locator = new OpenXmlExcelReportHistoryLocator(@"history\old");

            locator.ResolveOldReportsDirectoryPath().Should().Be(Path.Combine(tempDirectory, @"history\old"));
        }
        finally
        {
            Environment.CurrentDirectory = previousCurrentDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact(DisplayName = "ResolvePreviousReportPath returns the newest workbook in a directory")]
    [Trait("Category", "Unit")]
    public void ResolvePreviousReportPathReturnsNewestWorkbook()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var olderPath = Path.Combine(tempDirectory, "older.xlsx");
        var newerPath = Path.Combine(tempDirectory, "newer.xlsx");

        try
        {
            File.WriteAllText(olderPath, string.Empty);
            File.WriteAllText(newerPath, string.Empty);
            File.SetLastWriteTimeUtc(olderPath, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(newerPath, DateTime.UtcNow);

            OpenXmlExcelReportHistoryLocator.ResolvePreviousReportPath(tempDirectory).Should().Be(newerPath);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
