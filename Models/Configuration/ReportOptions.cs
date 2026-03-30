using System.ComponentModel.DataAnnotations;

namespace QAQueueManager.Models.Configuration;

/// <summary>
/// Represents report generation settings.
/// </summary>
internal sealed class ReportOptions
{
    /// <summary>
    /// Gets the report title.
    /// </summary>
    [Required]
    public string Title { get; init; } = "QA Queue By Repository";

    /// <summary>
    /// Gets the target branch used to validate merge status.
    /// </summary>
    [Required]
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets the configured PDF output path.
    /// </summary>
    [Required]
    public string PdfOutputPath { get; init; } = "qa-queue-report.pdf";

    /// <summary>
    /// Gets the configured Excel output path.
    /// </summary>
    [Required]
    public string ExcelOutputPath { get; init; } = "qa-queue-report.xlsx";

    /// <summary>
    /// Gets the optional path to a folder containing previous Excel reports used to restore manual markup.
    /// </summary>
    public string? OldReportsPath { get; init; }

    /// <summary>
    /// Gets the maximum number of code-linked issues processed in parallel.
    /// </summary>
    [Range(1, 32)]
    public int MaxParallelism { get; init; } = 4;

    /// <summary>
    /// Gets a value indicating whether no-code issues should be hidden from the report.
    /// </summary>
    public bool HideNoCodeIssues { get; init; }

    /// <summary>
    /// Gets a value indicating whether the generated PDF should be opened automatically.
    /// </summary>
    public bool OpenAfterGeneration { get; init; }
}
