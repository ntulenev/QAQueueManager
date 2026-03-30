using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Reads issue row snapshots from a worksheet so legacy markup can be restored.
/// </summary>
internal sealed class OpenXmlExcelWorksheetSnapshotReader
{
    private const string MARKUP_KEY_COLUMN_NAME = "MarkupKey";

    /// <summary>
    /// Reads issue row snapshots from the supplied worksheet.
    /// </summary>
    /// <param name="worksheetPart">The worksheet to inspect.</param>
    /// <returns>The discovered issue rows keyed by markup key.</returns>
    internal static Dictionary<string, OpenXmlExcelIssueRowSnapshot> Read(WorksheetPart worksheetPart)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);

        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Worksheet is missing.");
        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        var sharedStringTable = worksheetPart.GetParentParts()
            .OfType<WorkbookPart>()
            .Single()
            .SharedStringTablePart?
            .SharedStringTable;

        var rows = new Dictionary<string, OpenXmlExcelIssueRowSnapshot>(StringComparer.OrdinalIgnoreCase);
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

            rows[markupKey] = new OpenXmlExcelIssueRowSnapshot(
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

    private sealed record HeaderContext(int CommentColumnIndex, int MarkupKeyColumnIndex, int LastColumnIndex);
}
