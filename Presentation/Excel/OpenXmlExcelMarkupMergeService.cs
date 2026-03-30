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
/// Restores manual markup from previous workbooks into the generated Excel workbook.
/// </summary>
internal sealed class OpenXmlExcelMarkupMergeService : IExcelMarkupMergeService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenXmlExcelMarkupMergeService"/> class.
    /// </summary>
    /// <param name="reportOptions">The report configuration options.</param>
    public OpenXmlExcelMarkupMergeService(IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(reportOptions);

        _oldReportsPath = reportOptions.Value.OldReportsPath;
    }

    /// <inheritdoc />
    public ExcelMarkupMergeSummary Merge(Stream workbookStream, IReadOnlyDictionary<ExcelSheetName, ExcelSheetLayout> layouts)
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
            var builtInFillSignatures = GetFillSignatures(workbookPart.WorkbookStylesPart?.Stylesheet);

            foreach (var sheet in workbook.Sheets?.OfType<Sheet>() ?? [])
            {
                if (!ExcelSheetName.TryCreate(sheet.Name?.Value, out var sheetName) ||
                    !layouts.TryGetValue(sheetName, out var layout))
                {
                    continue;
                }

                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                if (previousWorkbook is not null)
                {
                    mergedRowKeys.AddRange(ApplyLegacyMarkup(previousWorkbook, workbookPart, worksheetPart, layout.Name, builtInFillSignatures));
                }
            }

            workbook.Save();
        }

        workbookStream.Position = 0;
        return new ExcelMarkupMergeSummary(
            resolvedOldReportsDirectoryPath,
            previousReportPath,
            [.. mergedRowKeys.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    private static List<string> ApplyLegacyMarkup(
        SpreadsheetDocument previousWorkbook,
        WorkbookPart currentWorkbookPart,
        WorksheetPart currentWorksheetPart,
        ExcelSheetName sheetName,
        ISet<string> builtInFillSignatures)
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

            var hasMergedChanges = TransferCommentValue(currentWorksheetPart, currentRow, previousRow.CommentValue);

            if (previousStylesheet is null)
            {
                if (hasMergedChanges)
                {
                    mergedRowKeys.Add(rowKey);
                }

                continue;
            }

            hasMergedChanges |= TransferFillStyles(
                currentWorksheetPart,
                currentStylesheet,
                previousStylesheet,
                builtInFillSignatures,
                currentRow,
                previousRow);

            if (hasMergedChanges)
            {
                mergedRowKeys.Add(rowKey);
            }
        }

        return mergedRowKeys;
    }

    private static bool TransferCommentValue(
        WorksheetPart worksheetPart,
        IssueRowSnapshot currentRow,
        string previousCommentValue)
    {
        if (currentRow.CommentColumnIndex <= 0 || string.IsNullOrWhiteSpace(previousCommentValue))
        {
            return false;
        }

        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        var commentCell = GetOrCreateCell(sheetData, currentRow.CommentColumnIndex, currentRow.RowIndex);
        var currentCommentValue = GetCellText(commentCell, sharedStringTable: null);
        if (string.Equals(currentCommentValue, previousCommentValue, StringComparison.Ordinal))
        {
            return false;
        }

        commentCell.DataType = CellValues.String;
        commentCell.CellValue = new CellValue(previousCommentValue);
        return true;
    }

    private static bool TransferFillStyles(
        WorksheetPart worksheetPart,
        Stylesheet currentStylesheet,
        Stylesheet previousStylesheet,
        ISet<string> builtInFillSignatures,
        IssueRowSnapshot currentRow,
        IssueRowSnapshot previousRow)
    {
        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        var lastColumnIndex = Math.Min(currentRow.LastColumnIndex, previousRow.LastColumnIndex);
        var hasMergedChanges = false;

        for (var columnIndex = 1; columnIndex <= lastColumnIndex; columnIndex++)
        {
            if (!previousRow.StyleIndexes.TryGetValue(columnIndex, out var previousStyleIndex))
            {
                continue;
            }

            var previousFill = GetFill(previousStylesheet, previousStyleIndex);
            if (previousFill is null)
            {
                continue;
            }

            var previousFillSignature = previousFill.OuterXml;
            if (string.IsNullOrWhiteSpace(previousFillSignature) || builtInFillSignatures.Contains(previousFillSignature))
            {
                continue;
            }

            var importedFillId = ImportFill(currentStylesheet, previousFill);
            var currentCell = GetOrCreateCell(sheetData, columnIndex, currentRow.RowIndex);
            var currentStyleIndex = currentCell.StyleIndex?.Value ?? 0U;
            var currentFillId = GetFillId(currentStylesheet, currentStyleIndex);
            if (currentFillId == importedFillId)
            {
                continue;
            }

            currentCell.StyleIndex = GetOrCreateCellFormatWithFill(currentStylesheet, currentStyleIndex, importedFillId);
            hasMergedChanges = true;
        }

        return hasMergedChanges;
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

    private static Fill? GetFill(Stylesheet stylesheet, uint styleIndex)
    {
        var fillId = GetFillId(stylesheet, styleIndex);
        return stylesheet.Fills?.Elements<Fill>().ElementAtOrDefault((int)fillId);
    }

    private static uint ImportFill(Stylesheet currentStylesheet, Fill previousFill)
    {
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

    private static HashSet<string> GetFillSignatures(Stylesheet? stylesheet)
    {
        if (stylesheet?.Fills is null)
        {
            return [];
        }

        return [.. stylesheet.Fills
            .Elements<Fill>()
            .Select(static fill => fill.OuterXml)
            .Where(static xml => !string.IsNullOrWhiteSpace(xml))];
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

    private static Cell GetOrCreateCell(SheetData sheetData, int columnIndex, int rowIndex)
    {
        var row = sheetData.Elements<Row>().FirstOrDefault(candidate => candidate.RowIndex?.Value == (uint)rowIndex);
        if (row is null)
        {
            row = new Row { RowIndex = (uint)rowIndex };
            var nextRow = sheetData.Elements<Row>().FirstOrDefault(candidate => candidate.RowIndex?.Value > (uint)rowIndex);
            _ = nextRow is null ? sheetData.AppendChild(row) : sheetData.InsertBefore(row, nextRow);
        }

        var cellReference = ToCellReference(columnIndex, rowIndex);
        var cell = row.Elements<Cell>().FirstOrDefault(candidate => string.Equals(candidate.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase));
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

        var nextCell = row.Elements<Cell>().FirstOrDefault(candidate => CompareCellReferences(candidate.CellReference?.Value, cellReference) > 0);
        _ = nextCell is null ? row.AppendChild(cell) : row.InsertBefore(cell, nextCell);
        return cell;
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

        return (column, int.Parse(cellReference[index..], CultureInfo.InvariantCulture));
    }

    private static string ToCellReference(int columnIndex, int rowIndex)
    {
        var dividend = columnIndex;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo, CultureInfo.InvariantCulture) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName + rowIndex.ToString(CultureInfo.InvariantCulture);
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

    private static SpreadsheetDocument? OpenPreviousWorkbook(string? previousReportPath)
    {
        if (string.IsNullOrWhiteSpace(previousReportPath))
        {
            return null;
        }

        return SpreadsheetDocument.Open(previousReportPath, false);
    }

    private readonly string? _oldReportsPath;
    private const string MARKUP_KEY_COLUMN_NAME = "MarkupKey";

    private sealed record HeaderContext(int CommentColumnIndex, int MarkupKeyColumnIndex, int LastColumnIndex);

    private sealed record IssueRowSnapshot(
        int RowIndex,
        int LastColumnIndex,
        int CommentColumnIndex,
        IReadOnlyDictionary<int, uint> StyleIndexes,
        string CommentValue);
}
