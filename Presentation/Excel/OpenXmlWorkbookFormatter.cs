using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Applies workbook formatting, hyperlinks, and cell styles using OpenXML.
/// </summary>
internal sealed class OpenXmlWorkbookFormatter : IWorkbookFormatter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenXmlWorkbookFormatter"/> class.
    /// </summary>
    /// <param name="reportOptions">The report configuration options.</param>
    public OpenXmlWorkbookFormatter(IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(reportOptions);

        _oldReportsPath = reportOptions.Value.OldReportsPath;
    }

    /// <summary>
    /// Formats the supplied workbook stream according to the specified layouts.
    /// </summary>
    /// <param name="workbookStream">The workbook stream to format.</param>
    /// <param name="layouts">Per-sheet layout metadata.</param>
    public ExcelMarkupMergeSummary Format(Stream workbookStream, IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(workbookStream);
        ArgumentNullException.ThrowIfNull(layouts);

        workbookStream.Position = 0;
        var resolvedOldReportsDirectoryPath = ResolveOldReportsDirectoryPath();
        var previousReportPath = ResolvePreviousReportPath(resolvedOldReportsDirectoryPath);
        var mergedRowKeys = new List<string>();
        using var previousWorkbook = OpenPreviousWorkbook(previousReportPath);
        using (var spreadsheet = SpreadsheetDocument.Open(workbookStream, true))
        {
            var workbookPart = spreadsheet.WorkbookPart
                ?? throw new InvalidOperationException("Workbook part is missing.");
            var workbook = workbookPart.Workbook
                ?? throw new InvalidOperationException("Workbook is missing.");

            EnsureStylesheet(workbookPart);

            foreach (var sheet in workbook.Sheets?.OfType<Sheet>() ?? [])
            {
                if (!ExcelSheetName.TryCreate(sheet.Name?.Value, out var sheetName) ||
                    !layouts.TryGetValue(sheetName, out var layout))
                {
                    continue;
                }

                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var worksheet = worksheetPart.Worksheet
                    ?? throw new InvalidOperationException("Worksheet is missing.");
                var sheetData = worksheet.GetFirstChild<SheetData>()
                    ?? throw new InvalidOperationException("Worksheet is missing sheet data.");

                ApplyColumnConfiguration(worksheet, layout.ColumnWidths, layout.HiddenColumns);
                ApplyTableStyles(sheetData, layout.TableRanges);
                ApplyCellStyles(sheetData, layout.CellStyles);
                ApplyHyperlinks(worksheetPart, layout.Hyperlinks);
                if (previousWorkbook is not null)
                {
                    mergedRowKeys.AddRange(ApplyLegacyMarkup(previousWorkbook, workbookPart, worksheetPart, layout.Name));
                }

                worksheet.Save();
            }

            workbook.Save();
        }

        workbookStream.Position = 0;
        return new ExcelMarkupMergeSummary(
            resolvedOldReportsDirectoryPath,
            previousReportPath,
            [.. mergedRowKeys.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    private static void EnsureStylesheet(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.WorkbookStylesPart ?? workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = CreateStylesheet();
        stylesPart.Stylesheet.Save();
    }

    private static Stylesheet CreateStylesheet()
    {
        var fonts = new Fonts(
            new Font(),
            new Font(new Bold(), new FontSize { Val = 16D }),
            new Font(new Bold(), new Color { Rgb = ToRgb("374151") }),
            new Font(new Bold(), new FontSize { Val = 12D }),
            new Font(new Color { Rgb = ToRgb("1D4ED8") }, new Underline()),
            new Font(new Color { Rgb = ToRgb("6B7280") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("C2410C") }))
        {
            Count = 7U,
            KnownFonts = true,
        };

        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(
                new ForegroundColor { Rgb = ToRgb("F3F4F6") },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(
                new ForegroundColor { Rgb = ToRgb("FFF7ED") },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid }))
        {
            Count = 4U,
        };

        var borders = new Borders(
            new Border(),
            new Border(
                new LeftBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = ToRgb("D1D5DB") } },
                new RightBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = ToRgb("D1D5DB") } },
                new TopBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = ToRgb("D1D5DB") } },
                new BottomBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = ToRgb("D1D5DB") } },
                new DiagonalBorder()))
        {
            Count = 2U,
        };

        var cellStyleFormats = new CellStyleFormats(new CellFormat()) { Count = 1U };
        var cellFormats = new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1U, ApplyFont = true },
            new CellFormat { FontId = 2U, ApplyFont = true },
            new CellFormat { FontId = 3U, ApplyFont = true },
            CreateBorderedFormat(fillId: 2U, fontId: 2U),
            CreateBorderedFormat(),
            CreateBorderedFormat(fontId: 4U),
            CreateBorderedFormat(fontId: 5U),
            CreateBorderedFormat(fontId: 6U, fillId: 3U))
        {
            Count = 9U,
        };

        return new Stylesheet(fonts, fills, borders, cellStyleFormats, cellFormats);
    }

    private static CellFormat CreateBorderedFormat(uint fontId = 0U, uint fillId = 0U) =>
        new()
        {
            FontId = fontId,
            FillId = fillId,
            BorderId = 1U,
            ApplyFont = true,
            ApplyFill = fillId != 0U,
            ApplyBorder = true,
            ApplyAlignment = true,
            Alignment = new Alignment
            {
                Vertical = VerticalAlignmentValues.Top,
                WrapText = true,
            },
        };

    private static void ApplyColumnConfiguration(
        Worksheet worksheet,
        Dictionary<int, double> columnWidths,
        HashSet<int> hiddenColumns)
    {
        if (columnWidths.Count == 0 && hiddenColumns.Count == 0)
        {
            return;
        }

        worksheet.RemoveAllChildren<Columns>();
        var columns = new Columns();
        foreach (var columnIndex in columnWidths.Keys.Union(hiddenColumns).OrderBy(static index => index))
        {
            var width = columnWidths.TryGetValue(columnIndex, out var configuredWidth)
                ? configuredWidth
                : DEFAULT_HIDDEN_COLUMN_WIDTH;
            columns.Append(new Column
            {
                Min = (uint)columnIndex,
                Max = (uint)columnIndex,
                Width = width,
                CustomWidth = true,
                Hidden = hiddenColumns.Contains(columnIndex),
            });
        }

        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        _ = worksheet.InsertBefore(columns, sheetData);
    }

    private static void ApplyTableStyles(SheetData sheetData, IEnumerable<ExcelTableRange> tableRanges)
    {
        foreach (var range in tableRanges)
        {
            for (var columnIndex = range.StartColumnIndex; columnIndex <= range.EndColumnIndex; columnIndex++)
            {
                SetCellStyle(sheetData, columnIndex, range.HeaderRow, ExcelCellStyleKind.Header);
            }

            for (var rowIndex = range.DataStartRow; rowIndex <= range.DataEndRow; rowIndex++)
            {
                for (var columnIndex = range.StartColumnIndex; columnIndex <= range.EndColumnIndex; columnIndex++)
                {
                    SetCellStyle(sheetData, columnIndex, rowIndex, ExcelCellStyleKind.Body);
                }
            }
        }
    }

    private static void ApplyCellStyles(SheetData sheetData, IReadOnlyDictionary<string, ExcelCellStyleKind> cellStyles)
    {
        foreach (var (cellReference, styleKind) in cellStyles)
        {
            var (columnIndex, rowIndex) = ParseCellReference(cellReference);
            SetCellStyle(sheetData, columnIndex, rowIndex, styleKind);
        }
    }

    private static void ApplyHyperlinks(WorksheetPart worksheetPart, Dictionary<string, string> hyperlinks)
    {
        if (hyperlinks.Count == 0)
        {
            return;
        }

        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Worksheet is missing.");
        var hyperlinksElement = worksheet.GetFirstChild<Hyperlinks>();
        if (hyperlinksElement is null)
        {
            hyperlinksElement = new Hyperlinks();
            var pageMargins = worksheet.GetFirstChild<PageMargins>();
            _ = pageMargins is not null
                ? worksheet.InsertBefore(hyperlinksElement, pageMargins)
                : worksheet.AppendChild(hyperlinksElement);
        }

        foreach (var (cellReference, targetUrl) in hyperlinks)
        {
            var relationship = worksheetPart.AddHyperlinkRelationship(new Uri(targetUrl, UriKind.Absolute), true);
            _ = hyperlinksElement.AppendChild(new Hyperlink
            {
                Reference = cellReference,
                Id = relationship.Id,
            });
        }
    }

    private static List<string> ApplyLegacyMarkup(
        SpreadsheetDocument previousWorkbook,
        WorkbookPart currentWorkbookPart,
        WorksheetPart currentWorksheetPart,
        ExcelSheetName sheetName)
    {
        var previousWorksheetPart = GetWorksheetPart(previousWorkbook.WorkbookPart, sheetName.Value);
        if (previousWorksheetPart is null)
        {
            return [];
        }

        var previousRows = ExtractIssueRows(previousWorksheetPart);
        if (previousRows.Count == 0)
        {
            return [];
        }

        var currentRows = ExtractIssueRows(currentWorksheetPart);
        if (currentRows.Count == 0)
        {
            return [];
        }

        var currentStylesheet = currentWorkbookPart.WorkbookStylesPart?.Stylesheet
            ?? throw new InvalidOperationException("Workbook stylesheet is missing.");
        var previousStylesheet = previousWorkbook.WorkbookPart?.WorkbookStylesPart?.Stylesheet;
        var mergedRowKeys = new List<string>();

        foreach (var (rowKey, currentRow) in currentRows)
        {
            if (!previousRows.TryGetValue(rowKey, out var previousRow))
            {
                continue;
            }

            mergedRowKeys.Add(rowKey);
            TransferCommentValue(currentWorksheetPart, currentRow, previousRow.CommentValue);

            if (previousStylesheet is null)
            {
                continue;
            }

            TransferFillStyles(
                currentWorksheetPart,
                currentStylesheet,
                previousStylesheet,
                currentRow,
                previousRow);
        }

        return mergedRowKeys;
    }

    private static void SetCellStyle(SheetData sheetData, int columnIndex, int rowIndex, ExcelCellStyleKind styleKind)
    {
        var cell = GetOrCreateCell(sheetData, columnIndex, rowIndex);
        cell.StyleIndex = (uint)styleKind;
    }

    private static Cell GetOrCreateCell(SheetData sheetData, int columnIndex, int rowIndex)
    {
        var row = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == (uint)rowIndex);
        if (row is null)
        {
            row = new Row { RowIndex = (uint)rowIndex };
            var nextRow = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value > (uint)rowIndex);
            _ = nextRow is null ? sheetData.AppendChild(row) : sheetData.InsertBefore(row, nextRow);
        }

        var cellReference = ToCellReference(columnIndex, rowIndex);
        var cell = row.Elements<Cell>().FirstOrDefault(c => string.Equals(c.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase));
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

        var nextCell = row.Elements<Cell>().FirstOrDefault(c => CompareCellReferences(c.CellReference?.Value, cellReference) > 0);
        _ = nextCell is null ? row.AppendChild(cell) : row.InsertBefore(cell, nextCell);
        return cell;
    }

    private static WorksheetPart? GetWorksheetPart(WorkbookPart? workbookPart, string sheetName)
    {
        if (workbookPart is null)
        {
            return null;
        }

        var workbook = workbookPart.Workbook;
        if (workbook is null || workbook.Sheets is null)
        {
            return null;
        }

        var sheet = workbook.Sheets
            .OfType<Sheet>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase));

        return sheet is null ? null : (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
    }

    private static Dictionary<string, IssueRowSnapshot> ExtractIssueRows(WorksheetPart worksheetPart)
    {
        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Worksheet is missing.");
        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        var sharedStringTable = worksheetPart.GetParentParts()
            .OfType<WorkbookPart>()
            .Single()
            .SharedStringTablePart?
            .SharedStringTable;

        var rows = new Dictionary<string, IssueRowSnapshot>(StringComparer.OrdinalIgnoreCase);
        HeaderContext? currentHeader = null;

        var inferredRowIndex = 0;
        foreach (var row in sheetData.Elements<Row>())
        {
            inferredRowIndex++;
            var rowIndex = checked((int)(row.RowIndex?.Value ?? (uint)inferredRowIndex));
            var nonEmptyColumns = GetNonEmptyColumns(row, sharedStringTable);

            if (nonEmptyColumns.Count == 0)
            {
                currentHeader = null;
                continue;
            }

            if (IsHeaderRow(nonEmptyColumns))
            {
                currentHeader = BuildHeaderContext(nonEmptyColumns);
                continue;
            }

            if (IsSingleValueRow(nonEmptyColumns))
            {
                currentHeader = null;
                continue;
            }

            if (currentHeader is null)
            {
                continue;
            }

            var markupKey = GetCellText(row, currentHeader.MarkupKeyColumnIndex, sharedStringTable).Trim();
            if (string.IsNullOrWhiteSpace(markupKey))
            {
                continue;
            }

            var styleIndexes = new Dictionary<int, uint>();
            for (var columnIndex = 1; columnIndex <= currentHeader.LastColumnIndex; columnIndex++)
            {
                var cell = GetCell(row, columnIndex);
                styleIndexes[columnIndex] = cell?.StyleIndex?.Value ?? 0U;
            }

            var commentValue = currentHeader.CommentColumnIndex > 0
                ? GetCellText(row, currentHeader.CommentColumnIndex, sharedStringTable)
                : string.Empty;

            rows[markupKey] = new IssueRowSnapshot(
                rowIndex,
                currentHeader.LastColumnIndex,
                currentHeader.CommentColumnIndex,
                styleIndexes,
                commentValue);
        }

        return rows;
    }

    private static Dictionary<int, string> GetNonEmptyColumns(Row row, SharedStringTable? sharedStringTable)
    {
        var result = new Dictionary<int, string>();
        foreach (var cell in row.Elements<Cell>())
        {
            var value = GetCellText(cell, sharedStringTable).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var cellReference = cell.CellReference?.Value;
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                continue;
            }

            result[ParseCellReference(cellReference).ColumnIndex] = value;
        }

        return result;
    }

    private static bool IsSingleValueRow(Dictionary<int, string> nonEmptyColumns) =>
        nonEmptyColumns.Count == 1 && nonEmptyColumns.ContainsKey(1);

    private static bool IsHeaderRow(Dictionary<int, string> nonEmptyColumns) =>
        nonEmptyColumns.TryGetValue(1, out var firstValue) &&
        string.Equals(firstValue, "#", StringComparison.Ordinal) &&
        nonEmptyColumns.Values.Any(static value => string.Equals(value, MARKUP_KEY_COLUMN_NAME, StringComparison.OrdinalIgnoreCase));

    private static HeaderContext BuildHeaderContext(Dictionary<int, string> nonEmptyColumns)
    {
        var commentColumnIndex = 0;
        var markupKeyColumnIndex = 0;

        foreach (var (columnIndex, value) in nonEmptyColumns)
        {
            if (string.Equals(value, "Comment", StringComparison.OrdinalIgnoreCase))
            {
                commentColumnIndex = columnIndex;
            }

            if (string.Equals(value, MARKUP_KEY_COLUMN_NAME, StringComparison.OrdinalIgnoreCase))
            {
                markupKeyColumnIndex = columnIndex;
            }
        }

        return new HeaderContext(
            commentColumnIndex,
            markupKeyColumnIndex,
            nonEmptyColumns.Keys.Max());
    }

    private static void TransferCommentValue(
        WorksheetPart worksheetPart,
        IssueRowSnapshot currentRow,
        string previousCommentValue)
    {
        if (currentRow.CommentColumnIndex <= 0 || string.IsNullOrWhiteSpace(previousCommentValue))
        {
            return;
        }

        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        var commentCell = GetOrCreateCell(sheetData, currentRow.CommentColumnIndex, currentRow.RowIndex);
        commentCell.DataType = CellValues.String;
        commentCell.CellValue = new CellValue(previousCommentValue);
    }

    private static void TransferFillStyles(
        WorksheetPart worksheetPart,
        Stylesheet currentStylesheet,
        Stylesheet previousStylesheet,
        IssueRowSnapshot currentRow,
        IssueRowSnapshot previousRow)
    {
        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        var lastColumnIndex = Math.Min(currentRow.LastColumnIndex, previousRow.LastColumnIndex);

        for (var columnIndex = 1; columnIndex <= lastColumnIndex; columnIndex++)
        {
            if (!previousRow.StyleIndexes.TryGetValue(columnIndex, out var previousStyleIndex))
            {
                continue;
            }

            var previousFillId = GetFillId(previousStylesheet, previousStyleIndex);
            if (previousFillId <= 1)
            {
                continue;
            }

            var importedFillId = ImportFill(currentStylesheet, previousStylesheet, previousFillId);
            var currentCell = GetOrCreateCell(sheetData, columnIndex, currentRow.RowIndex);
            var currentStyleIndex = currentCell.StyleIndex?.Value ?? 0U;
            currentCell.StyleIndex = GetOrCreateCellFormatWithFill(currentStylesheet, currentStyleIndex, importedFillId);
        }
    }

    private static uint GetFillId(Stylesheet stylesheet, uint styleIndex)
    {
        var cellFormats = stylesheet.CellFormats?.Elements<CellFormat>().ToList() ?? [];
        if (styleIndex >= cellFormats.Count)
        {
            return 0U;
        }

        return cellFormats[(int)styleIndex].FillId?.Value ?? 0U;
    }

    private static uint ImportFill(Stylesheet currentStylesheet, Stylesheet previousStylesheet, uint previousFillId)
    {
        var previousFill = previousStylesheet.Fills?.Elements<Fill>().ElementAtOrDefault((int)previousFillId)
            ?? throw new InvalidOperationException("Referenced fill is missing in the previous workbook.");
        var fills = currentStylesheet.Fills ?? throw new InvalidOperationException("Workbook fills are missing.");

        var existing = fills.Elements<Fill>()
            .Select((fill, index) => new { fill, index })
            .FirstOrDefault(candidate => string.Equals(candidate.fill.OuterXml, previousFill.OuterXml, StringComparison.Ordinal));
        if (existing is not null)
        {
            return (uint)existing.index;
        }

        _ = fills.AppendChild((Fill)previousFill.CloneNode(true));
        var fillCount = (uint)fills.Elements<Fill>().Count();
        fills.Count = fillCount;
        return fillCount - 1;
    }

    private static uint GetOrCreateCellFormatWithFill(
        Stylesheet stylesheet,
        uint baseStyleIndex,
        uint fillId)
    {
        var cellFormats = stylesheet.CellFormats ?? throw new InvalidOperationException("Workbook cell formats are missing.");
        var baseFormat = cellFormats.Elements<CellFormat>().ElementAtOrDefault((int)baseStyleIndex) ?? new CellFormat();
        var updatedFormat = (CellFormat)baseFormat.CloneNode(true);
        updatedFormat.FillId = fillId;
        updatedFormat.ApplyFill = true;

        var existing = cellFormats.Elements<CellFormat>()
            .Select((format, index) => new { format, index })
            .FirstOrDefault(candidate => string.Equals(candidate.format.OuterXml, updatedFormat.OuterXml, StringComparison.Ordinal));
        if (existing is not null)
        {
            return (uint)existing.index;
        }

        _ = cellFormats.AppendChild(updatedFormat);
        var formatCount = (uint)cellFormats.Elements<CellFormat>().Count();
        cellFormats.Count = formatCount;
        return formatCount - 1;
    }

    private static Cell? GetCell(Row row, int columnIndex) =>
        row.Elements<Cell>()
            .FirstOrDefault(cell =>
            {
                var cellReference = cell.CellReference?.Value;
                return !string.IsNullOrWhiteSpace(cellReference) &&
                    ParseCellReference(cellReference).ColumnIndex == columnIndex;
            });

    private static string GetCellText(Row row, int columnIndex, SharedStringTable? sharedStringTable)
    {
        var cell = GetCell(row, columnIndex);
        return cell is null ? string.Empty : GetCellText(cell, sharedStringTable);
    }

    private static string GetCellText(Cell cell, SharedStringTable? sharedStringTable)
    {
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex) &&
            sharedStringTable is not null)
        {
            return sharedStringTable.Elements<SharedStringItem>().ElementAt(sharedStringIndex).InnerText;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        return cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
    }

    private static SpreadsheetDocument? OpenPreviousWorkbook(string? previousReportPath)
    {
        if (string.IsNullOrWhiteSpace(previousReportPath))
        {
            return null;
        }

        return SpreadsheetDocument.Open(previousReportPath, false);
    }

    private string? ResolveOldReportsDirectoryPath()
    {
        if (string.IsNullOrWhiteSpace(_oldReportsPath))
        {
            return null;
        }

        return Path.IsPathRooted(_oldReportsPath)
            ? _oldReportsPath
            : Path.Combine(Environment.CurrentDirectory, _oldReportsPath);
    }

    private static string? ResolvePreviousReportPath(string? resolvedDirectory)
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

    private static int CompareCellReferences(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return 1;
        }

        var (leftColumn, leftRow) = ParseCellReference(left);
        var (rightColumn, rightRow) = ParseCellReference(right);
        var rowComparison = leftRow.CompareTo(rightRow);
        return rowComparison != 0 ? rowComparison : leftColumn.CompareTo(rightColumn);
    }

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

        var row = int.Parse(cellReference[index..], CultureInfo.InvariantCulture);
        return (column, row);
    }

    private static string ToCellReference(int columnIndex, int rowIndex) =>
        ToColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);

    private static string ToColumnName(int columnIndex)
    {
        var dividend = columnIndex;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo, CultureInfo.InvariantCulture) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string ToRgb(string value) => value.TrimStart('#').ToUpperInvariant();

    private readonly string? _oldReportsPath;
    private const string MARKUP_KEY_COLUMN_NAME = "MarkupKey";
    private const double DEFAULT_HIDDEN_COLUMN_WIDTH = 18D;

    private sealed record HeaderContext(int CommentColumnIndex, int MarkupKeyColumnIndex, int LastColumnIndex);

    private sealed record IssueRowSnapshot(
        int RowIndex,
        int LastColumnIndex,
        int CommentColumnIndex,
        IReadOnlyDictionary<int, uint> StyleIndexes,
        string CommentValue);
}
