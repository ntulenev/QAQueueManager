namespace QAQueueManager.Models.Rendering;

/// <summary>
/// Represents a rectangular Excel table range within a worksheet.
/// </summary>
/// <param name="HeaderRow">The header row index.</param>
/// <param name="StartColumnIndex">The first column index.</param>
/// <param name="EndColumnIndex">The last column index.</param>
/// <param name="DataStartRow">The first data row index.</param>
/// <param name="DataEndRow">The last data row index.</param>
internal sealed record ExcelTableRange(
    int HeaderRow,
    int StartColumnIndex,
    int EndColumnIndex,
    int DataStartRow,
    int DataEndRow);
