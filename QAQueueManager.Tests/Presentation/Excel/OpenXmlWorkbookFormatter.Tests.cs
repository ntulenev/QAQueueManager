using System.Globalization;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using FluentAssertions;

using Microsoft.Extensions.Options;

using MiniExcelLibs;

using QAQueueManager.Models.Configuration;
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
        layout.HiddenColumns.Add(3);
        layout.TableRanges.Add(new ExcelTableRange(1, 1, 2, 2, 2));
        layout.CellStyles["B2"] = ExcelCellStyleKind.Hyperlink;
        layout.Hyperlinks["B2"] = "https://jira.example.test/browse/QA-1";
        var formatter = CreateFormatter();

        // Act
        var summary = formatter.Format(stream, new Dictionary<ExcelSheetName, ExcelSheetLayout>
        {
            [new ExcelSheetName("Sheet1")] = layout
        });

        // Assert
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet was not generated.");
        var columns = worksheet.Elements<Columns>().Single().Elements<Column>().ToList();
        var visibleColumn = columns.Single(column => column.Min?.Value == 1U);
        visibleColumn.Width?.Value.Should().Be(20D);
        visibleColumn.Hidden?.Value.Should().NotBe(true);
        var hiddenColumn = columns.Single(column => column.Min?.Value == 3U);
        hiddenColumn.Hidden?.Value.Should().BeTrue();
        worksheet.Descendants<Hyperlink>().Should().ContainSingle(link => link.Reference == "B2");
        worksheet.Descendants<Cell>().Should().Contain(cell => cell.CellReference == "B2" && cell.StyleIndex!.Value == (uint)ExcelCellStyleKind.Hyperlink);
        summary.PreviousReportPath.Should().BeNull();
        summary.MergedRowKeys.Should().BeEmpty();
    }

    [Fact(DisplayName = "Format restores row fill and comment from newest old workbook")]
    [Trait("Category", "Unit")]
    public void FormatRestoresRowFillAndCommentFromNewestOldWorkbook()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var oldWorkbookPath = Path.Combine(tempDirectory, "old-report.xlsx");

        try
        {
            using (var oldStream = CreateIssueWorkbook("Needs retest"))
            using (var fileStream = File.Create(oldWorkbookPath))
            {
                oldStream.CopyTo(fileStream);
            }

            ApplyManualFill(oldWorkbookPath, "Sheet1", "A5", "E5");
            File.SetLastWriteTimeUtc(oldWorkbookPath, DateTime.UtcNow);

            using var newStream = CreateIssueWorkbook(string.Empty);
            var formatter = CreateFormatter(tempDirectory);
            var layout = new ExcelSheetLayout(new ExcelSheetName("Sheet1"));
            layout.HiddenColumns.Add(5);

            // Act
            var summary = formatter.Format(newStream, new Dictionary<ExcelSheetName, ExcelSheetLayout>
            {
                [new ExcelSheetName("Sheet1")] = layout
            });

            // Assert
            using var document = SpreadsheetDocument.Open(newStream, false);
            var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook part is missing.");
            var worksheetPart = workbookPart.WorksheetParts.Single();
            var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet is missing.");
            var commentCell = worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "D5");
            commentCell.InnerText.Should().Be("Needs retest");
            var hiddenColumn = worksheet.Elements<Columns>().Single().Elements<Column>().Single(column => column.Min?.Value == 5U);
            hiddenColumn.Hidden?.Value.Should().BeTrue();

            var cellFormats = workbookPart.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ToList();
            var fills = workbookPart.WorkbookStylesPart!.Stylesheet!.Fills!.Elements<Fill>().ToList();
            var highlightedCell = worksheet.Descendants<Cell>().Single(cell => cell.CellReference == "A5");
            var fillId = cellFormats[(int)highlightedCell.StyleIndex!.Value].FillId!.Value;
            fills[(int)fillId].PatternFill!.ForegroundColor!.Rgb!.Value.Should().Be("F59E0B");
            summary.OldReportsDirectoryPath.Should().Be(tempDirectory);
            summary.PreviousReportPath.Should().Be(oldWorkbookPath);
            summary.MergedRowKeys.Should().ContainSingle().Which.Should().Be("Sheet1|workspace/service-a|QA-1|1.2.3");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact(DisplayName = "Format does not report merged rows when old workbook has no manual markup to restore")]
    [Trait("Category", "Unit")]
    public void FormatWhenOldWorkbookHasNoManualMarkupDoesNotReportMergedRows()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var oldWorkbookPath = Path.Combine(tempDirectory, "old-report.xlsx");

        try
        {
            using (var oldStream = CreateIssueWorkbook(commentValue: null))
            using (var fileStream = File.Create(oldWorkbookPath))
            {
                oldStream.CopyTo(fileStream);
            }

            File.SetLastWriteTimeUtc(oldWorkbookPath, DateTime.UtcNow);

            using var newStream = CreateIssueWorkbook(string.Empty);
            var formatter = CreateFormatter(tempDirectory);
            var layout = new ExcelSheetLayout(new ExcelSheetName("Sheet1"));
            layout.HiddenColumns.Add(5);

            // Act
            var summary = formatter.Format(newStream, new Dictionary<ExcelSheetName, ExcelSheetLayout>
            {
                [new ExcelSheetName("Sheet1")] = layout
            });

            // Assert
            summary.OldReportsDirectoryPath.Should().Be(tempDirectory);
            summary.PreviousReportPath.Should().Be(oldWorkbookPath);
            summary.MergedRowKeys.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static OpenXmlWorkbookFormatter CreateFormatter(string? oldReportsPath = null) =>
        new(Options.Create(new ReportOptions
        {
            Title = "QA Queue",
            TargetBranch = "main",
            OldReportsPath = oldReportsPath
        }));

    private static MemoryStream CreateIssueWorkbook(string? commentValue)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var dataRow = new List<(string CellReference, string Value)>
            {
                ("A5", "1"),
                ("B5", "QA-1"),
                ("C5", "Open"),
                ("E5", "Sheet1|workspace/service-a|QA-1|1.2.3"),
            };
            if (commentValue is not null)
            {
                dataRow.Insert(3, ("D5", commentValue));
            }

            var sheetData = new SheetData(
                CreateRow(1, ("A1", "QA Queue | Team: Core")),
                CreateRow(2, ("A2", "workspace/service-a")),
                CreateRow(3, ("A3", "Tasks merged into target branch")),
                CreateRow(4, ("A4", "#"), ("B4", "Issue"), ("C4", "Status"), ("D4", "Comment"), ("E4", "MarkupKey")),
                CreateRow(5, [.. dataRow]));
            worksheetPart.Worksheet = new Worksheet(sheetData);

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

    private static void ApplyManualFill(string workbookPath, string sheetName, string startCell, string endCell)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, true);
        ApplyManualFill(document, sheetName, startCell, endCell);
    }

    private static void ApplyManualFill(SpreadsheetDocument document, string sheetName, string startCell, string endCell)
    {
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook part is missing.");
        var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet ?? CreateStylesheet(workbookPart);
        var fillId = AppendFill(stylesheet, CreateSolidFill("F59E0B"));
        var styleIndex = AppendCellFormat(stylesheet, new CellFormat
        {
            FillId = fillId,
            ApplyFill = true,
        });

        var workbook = workbookPart.Workbook ?? throw new InvalidOperationException("Workbook is missing.");
        var sheets = workbook.Sheets ?? throw new InvalidOperationException("Workbook sheets are missing.");
        var sheet = sheets.OfType<Sheet>().Single(candidate => candidate.Name == sheetName);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet is missing.");
        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");

        var (startColumnIndex, rowIndex) = ParseCellReference(startCell);
        var (endColumnIndex, _) = ParseCellReference(endCell);
        for (var columnIndex = startColumnIndex; columnIndex <= endColumnIndex; columnIndex++)
        {
            var cell = GetOrCreateCell(sheetData, columnIndex, rowIndex);
            cell.StyleIndex = styleIndex;
        }

        worksheet.Save();
        stylesheet.Save();
    }

    private static Stylesheet CreateStylesheet(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.WorkbookStylesPart ?? workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new Fonts(new Font()) { Count = 1U, KnownFonts = true },
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(
                    new ForegroundColor { Rgb = "F3F4F6" },
                    new BackgroundColor { Indexed = 64U })
                { PatternType = PatternValues.Solid }),
                new Fill(new PatternFill(
                    new ForegroundColor { Rgb = "FFF7ED" },
                    new BackgroundColor { Indexed = 64U })
                { PatternType = PatternValues.Solid }))
            { Count = 4U },
            new Borders(new Border()) { Count = 1U },
            new CellStyleFormats(new CellFormat()) { Count = 1U },
            new CellFormats(new CellFormat()) { Count = 1U });
        return stylesPart.Stylesheet;
    }

    private static uint AppendFill(Stylesheet stylesheet, Fill fill)
    {
        var fills = stylesheet.Fills ?? throw new InvalidOperationException("Workbook fills are missing.");
        _ = fills.AppendChild(fill);
        var fillCount = (uint)fills.Elements<Fill>().Count();
        fills.Count = fillCount;
        return fillCount - 1;
    }

    private static uint AppendCellFormat(Stylesheet stylesheet, CellFormat cellFormat)
    {
        var cellFormats = stylesheet.CellFormats ?? throw new InvalidOperationException("Workbook cell formats are missing.");
        _ = cellFormats.AppendChild(cellFormat);
        var formatCount = (uint)cellFormats.Elements<CellFormat>().Count();
        cellFormats.Count = formatCount;
        return formatCount - 1;
    }

    private static Fill CreateSolidFill(string rgb) =>
        new(new PatternFill(
            new ForegroundColor { Rgb = rgb },
            new BackgroundColor { Indexed = 64U })
        { PatternType = PatternValues.Solid });

    private static (int ColumnIndex, int RowIndex) ParseCellReference(string cellReference)
    {
        var column = 0;
        var index = 0;
        while (index < cellReference.Length && char.IsLetter(cellReference[index]))
        {
            var letterValue = char.ToUpperInvariant(cellReference[index]) - 'A' + 1;
            column = (column * 26) + letterValue;
            index++;
        }

        return (column, int.Parse(cellReference[index..], CultureInfo.InvariantCulture));
    }

    private static Cell GetOrCreateCell(SheetData sheetData, int columnIndex, int rowIndex)
    {
        var row = sheetData.Elements<Row>().Single(candidate => candidate.RowIndex?.Value == (uint)rowIndex);
        var cellReference = ToCellReference(columnIndex, rowIndex);
        var cell = row.Elements<Cell>().FirstOrDefault(candidate => candidate.CellReference == cellReference);
        if (cell is not null)
        {
            return cell;
        }

        cell = new Cell
        {
            CellReference = cellReference,
            DataType = CellValues.String,
            CellValue = new CellValue(string.Empty),
        };
        row.AppendChild(cell);
        return cell;
    }

    private static string ToCellReference(int columnIndex, int rowIndex)
    {
        var dividend = columnIndex;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName + rowIndex.ToString(CultureInfo.InvariantCulture);
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
