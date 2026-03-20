using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using FluentAssertions;

using MiniExcelLibs;

using QAQueueManager.Models.Rendering;
using QAQueueManager.Presentation.Excel;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class OpenXmlWorkbookFormatterTests
{
    [Fact(DisplayName = "Format applies widths styles and hyperlinks to workbook sheets")]
    [Trait("Category", "Unit")]
    public void FormatAppliesWidthsStylesAndHyperlinksToWorkbookSheets()
    {
        // Arrange
        using var stream = new MemoryStream();
        MiniExcel.SaveAs(
            stream,
            new Dictionary<string, object>
            {
                ["Sheet1"] = new[]
                {
                    new Dictionary<string, object?> { ["C1"] = "Title", ["C2"] = "Link" },
                    new Dictionary<string, object?> { ["C1"] = "Value", ["C2"] = "Open" }
                }
            },
            printHeader: false);
        var layout = new ExcelSheetLayout(new ExcelSheetName("Sheet1"));
        layout.ColumnWidths[1] = 20;
        layout.TableRanges.Add(new ExcelTableRange(1, 1, 2, 2, 2));
        layout.CellStyles["B2"] = ExcelCellStyleKind.Hyperlink;
        layout.Hyperlinks["B2"] = "https://jira.example.test/browse/QA-1";
        var formatter = new OpenXmlWorkbookFormatter();

        // Act
        formatter.Format(stream, new Dictionary<ExcelSheetName, ExcelSheetLayout>
        {
            [new ExcelSheetName("Sheet1")] = layout
        });

        // Assert
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet was not generated.");
        worksheet.Elements<Columns>().Single().Elements<Column>().Should().ContainSingle(column => column.Width!.Value == 20D);
        worksheet.Descendants<Hyperlink>().Should().ContainSingle(link => link.Reference == "B2");
        worksheet.Descendants<Cell>().Should().Contain(cell => cell.CellReference == "B2" && cell.StyleIndex!.Value == (uint)ExcelCellStyleKind.Hyperlink);
    }
}
