using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Builds the final QA queue report from processed domain inputs.
/// </summary>
internal interface IQaQueueReportBuilder
{
    /// <summary>
    /// Builds the QA queue report from already classified and processed issues.
    /// </summary>
    /// <param name="noCodeIssues">The issues without code links.</param>
    /// <param name="processedIssues">The processed code-linked issues.</param>
    /// <returns>The generated QA queue report.</returns>
    QaQueueReport Build(
        IReadOnlyList<QaIssue> noCodeIssues,
        IReadOnlyList<ProcessedCodeIssue> processedIssues);
}
