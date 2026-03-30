namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Captures the parts of an Excel issue row that are relevant to markup restoration.
/// </summary>
/// <param name="RowIndex">The one-based row index.</param>
/// <param name="LastColumnIndex">The last column index present in the row.</param>
/// <param name="CommentColumnIndex">The column index that contains the comment cell.</param>
/// <param name="StyleIndexes">The original style indexes for the row cells.</param>
/// <param name="CommentValue">The existing comment value.</param>
internal sealed record OpenXmlExcelIssueRowSnapshot(
    int RowIndex,
    int LastColumnIndex,
    int CommentColumnIndex,
    IReadOnlyDictionary<int, uint> StyleIndexes,
    string CommentValue);
