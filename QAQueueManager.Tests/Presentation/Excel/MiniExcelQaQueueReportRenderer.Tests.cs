using FluentAssertions;

using Moq;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;
using QAQueueManager.Presentation.Excel;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class MiniExcelQaQueueReportRendererTests
{
    [Fact(DisplayName = "Render creates workbook stream and passes layout metadata to formatter")]
    [Trait("Category", "Unit")]
    public void RenderCreatesWorkbookStreamAndPassesLayoutMetadataToFormatter()
    {
        // Arrange
        var report = TestData.CreateReport();
        var workbook = new ExcelWorkbookData(
            new Dictionary<ExcelSheetName, object>
            {
                [new ExcelSheetName("Sheet1")] = new[] { new Dictionary<string, object?> { ["C1"] = "Value" } }
            },
            new Dictionary<ExcelSheetName, ExcelSheetLayout>
            {
                [new ExcelSheetName("Sheet1")] = new ExcelSheetLayout(new ExcelSheetName("Sheet1"))
            });
        var formatterCalls = 0;

        var composer = new Mock<IExcelWorkbookContentComposer>(MockBehavior.Strict);
        composer.Setup(value => value.ComposeWorkbook(It.Is<QaQueueReport>(candidate => candidate == report)))
            .Callback(() => { })
            .Returns(workbook);

        var formatter = new Mock<IWorkbookFormatter>(MockBehavior.Strict);
        formatter.Setup(value => value.Format(
                It.Is<Stream>(stream => stream.CanSeek && stream.Length > 0),
                It.Is<IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout>>(layouts =>
                    layouts.Count == 1 && layouts.ContainsKey(new ExcelSheetName("Sheet1")))))
            .Callback(() => formatterCalls++);

        var renderer = new MiniExcelQaQueueReportRenderer(composer.Object, formatter.Object);

        // Act
        using var stream = renderer.Render(report);

        // Assert
        stream.Length.Should().BeGreaterThan(0);
        formatterCalls.Should().Be(1);
    }
}
