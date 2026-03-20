using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation.Pdf;

namespace QAQueueManager.Tests.Presentation.Pdf;

public sealed class PdfReportFileStoreTests
{
    [Fact(DisplayName = "PdfReportFileStore saves content to timestamped PDF path")]
    [Trait("Category", "Unit")]
    public void PdfReportFileStoreSavesContentToTimestampedPdfPath()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var store = new PdfReportFileStore();
        var originalDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = tempDirectory;

        try
        {
            // Act
            var path = store.Save([1, 2, 3, 4], new ReportFilePath("reports\\qa-report"));

            // Assert
            File.Exists(path.Value).Should().BeTrue();
            path.Value.Should().EndWith(".pdf");
            File.ReadAllBytes(path.Value).Should().Equal([1, 2, 3, 4]);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
