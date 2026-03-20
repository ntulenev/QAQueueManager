using FluentAssertions;

using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Tests.Models.Rendering;

public sealed class ExcelWorkbookDataTests
{
    [Fact(DisplayName = "ExcelWorkbookData exposes supplied workbook sheets and layouts")]
    [Trait("Category", "Unit")]
    public void ExcelWorkbookDataExposesSuppliedWorkbookSheetsAndLayouts()
    {
        // Arrange
        var sheetName = new ExcelSheetName("Core Team");
        var layout = new ExcelSheetLayout(sheetName);
        var workbook = new ExcelWorkbookData(
            new Dictionary<ExcelSheetName, object> { [sheetName] = new[] { new { C1 = "Value" } } },
            new Dictionary<ExcelSheetName, ExcelSheetLayout> { [sheetName] = layout });

        // Assert
        workbook.Sheets.Should().ContainKey(sheetName);
        workbook.Layouts.Should().ContainKey(sheetName).WhoseValue.Should().Be(layout);
    }
}
