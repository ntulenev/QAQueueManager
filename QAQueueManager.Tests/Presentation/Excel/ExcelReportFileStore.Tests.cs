using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation.Excel;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class ExcelReportFileStoreTests
{
    [Fact(DisplayName = "ExcelReportFileStore saves workbook content to timestamped path")]
    [Trait("Category", "Unit")]
    public void ExcelReportFileStoreSavesWorkbookContentToTimestampedPath()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        using var stream = new MemoryStream([1, 2, 3, 4]);
        var store = new ExcelReportFileStore();
        var originalDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = tempDirectory;

        try
        {
            // Act
            var path = store.Save(stream, new ReportFilePath("reports\\qa-report"));

            // Assert
            File.Exists(path.Value).Should().BeTrue();
            path.Value.Should().EndWith(".xlsx");
            File.ReadAllBytes(path.Value).Should().Equal([1, 2, 3, 4]);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
