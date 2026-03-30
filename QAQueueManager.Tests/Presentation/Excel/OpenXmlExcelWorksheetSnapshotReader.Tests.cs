using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using FluentAssertions;

using QAQueueManager.Presentation.Excel;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class OpenXmlExcelWorksheetSnapshotReaderTests
{
    [Fact(DisplayName = "Read captures row styles and comment metadata from issue rows")]
    [Trait("Category", "Unit")]
    public void ReadCapturesRowStylesAndCommentMetadata()
    {
        using var stream = CreateWorkbook();
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();

        var rows = OpenXmlExcelWorksheetSnapshotReader.Read(worksheetPart);

        rows.Should().ContainKey("Sheet1|workspace/service-a|QA-1|1.2.3");
        var snapshot = rows["Sheet1|workspace/service-a|QA-1|1.2.3"];
        snapshot.RowIndex.Should().Be(5);
        snapshot.LastColumnIndex.Should().Be(5);
        snapshot.CommentColumnIndex.Should().Be(4);
        snapshot.CommentValue.Should().Be("Needs retest");
        snapshot.StyleIndexes.Should().ContainKey(1).WhoseValue.Should().Be(3U);
        snapshot.StyleIndexes.Should().ContainKey(4).WhoseValue.Should().Be(4U);
    }

    private static MemoryStream CreateWorkbook()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet(
                new Fonts(new Font()) { Count = 1U, KnownFonts = true },
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
                {
                    Count = 2U
                },
                new Borders(new Border()) { Count = 1U },
                new CellStyleFormats(new CellFormat()) { Count = 1U },
                new CellFormats(
                    new CellFormat(),
                    new CellFormat(),
                    new CellFormat(),
                    new CellFormat { FillId = 2U, ApplyFill = true },
                    new CellFormat { FillId = 3U, ApplyFill = true })
                {
                    Count = 5U
                });

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(
                new SheetData(
                    CreateRow(1, ("A1", "QA Queue | Team: Core")),
                    CreateRow(2, ("A2", "workspace/service-a")),
                    CreateRow(3, ("A3", "Tasks merged into target branch")),
                    CreateRow(4, ("A4", "#", null), ("B4", "Issue", null), ("C4", "Status", null), ("D4", "Comment", null), ("E4", "MarkupKey", null)),
                    CreateRow(5, ("A5", "1", 3U), ("B5", "QA-1", null), ("C5", "Open", null), ("D5", "Needs retest", 4U), ("E5", "Sheet1|workspace/service-a|QA-1|1.2.3", null))));

            workbookPart.Workbook.AppendChild(new Sheets(
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1U,
                    Name = "Sheet1",
                }));
            workbookPart.Workbook.Save();
            worksheetPart.Worksheet.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static Row CreateRow(int rowIndex, params (string CellReference, string Value, uint? StyleIndex)[] values)
    {
        var row = new Row { RowIndex = (uint)rowIndex };
        foreach (var (cellReference, value, styleIndex) in values)
        {
            row.AppendChild(new Cell
            {
                CellReference = cellReference,
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value)),
                StyleIndex = styleIndex,
            });
        }

        return row;
    }

    private static Row CreateRow(int rowIndex, params (string CellReference, string Value)[] values)
    {
        var row = new Row { RowIndex = (uint)rowIndex };
        foreach (var (cellReference, value) in values)
        {
            row.AppendChild(new Cell
            {
                CellReference = cellReference,
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value)),
            });
        }

        return row;
    }
}
