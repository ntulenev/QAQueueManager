namespace QAQueueManager.Models.Domain;

/// <summary>
/// Describes how manual Excel markup was restored from a previous workbook.
/// </summary>
/// <param name="OldReportsDirectoryPath">The resolved directory scanned for old reports.</param>
/// <param name="PreviousReportPath">The previous workbook used for markup restore.</param>
/// <param name="MergedRowKeys">The row keys that received restored markup.</param>
internal sealed record ExcelMarkupMergeSummary(
    string? OldReportsDirectoryPath,
    string? PreviousReportPath,
    IReadOnlyList<string> MergedRowKeys);
