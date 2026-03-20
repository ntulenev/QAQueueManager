using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Builds the domain report used by all output formats.
/// </summary>
internal interface IQaQueueReportService
{
    /// <summary>
    /// Builds the QA queue report from Jira and Bitbucket data.
    /// </summary>
    /// <param name="progress">An optional progress sink for interactive UI updates.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The generated QA queue report.</returns>
    Task<QaQueueReport> BuildAsync(
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken);
}
