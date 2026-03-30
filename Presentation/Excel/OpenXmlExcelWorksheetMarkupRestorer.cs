using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Restores comments and manual fills from a previous workbook into the current workbook.
/// </summary>
internal sealed class OpenXmlExcelWorksheetMarkupRestorer
{
    /// <summary>
    /// Restores manual markup for one worksheet and returns the merged row keys.
    /// </summary>
    /// <param name="previousWorkbook">The previous workbook used as the markup source.</param>
    /// <param name="currentWorkbookPart">The current workbook part.</param>
    /// <param name="currentWorksheetPart">The current worksheet part.</param>
    /// <param name="sheetName">The worksheet name to restore.</param>
    /// <param name="builtInFillSignatures">The baseline fill signatures of the generated workbook.</param>
    /// <returns>The row keys whose markup was restored.</returns>
    internal static IReadOnlyList<string> Restore(
        SpreadsheetDocument previousWorkbook,
        WorkbookPart currentWorkbookPart,
        WorksheetPart currentWorksheetPart,
        ExcelSheetName sheetName,
        ISet<string> builtInFillSignatures)
    {
        ArgumentNullException.ThrowIfNull(previousWorkbook);
        ArgumentNullException.ThrowIfNull(currentWorkbookPart);
        ArgumentNullException.ThrowIfNull(currentWorksheetPart);
        ArgumentNullException.ThrowIfNull(builtInFillSignatures);

        var previousWorksheetPart = GetWorksheetPart(previousWorkbook.WorkbookPart, sheetName.Value);
        if (previousWorksheetPart is null)
        {
            return [];
        }

        var previousRows = OpenXmlExcelWorksheetSnapshotReader.Read(previousWorksheetPart);
        if (previousRows.Count == 0)
        {
            return [];
        }

        var currentRows = OpenXmlExcelWorksheetSnapshotReader.Read(currentWorksheetPart);
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
        OpenXmlExcelIssueRowSnapshot currentRow,
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
        OpenXmlExcelIssueRowSnapshot currentRow,
        OpenXmlExcelIssueRowSnapshot previousRow)
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

}
