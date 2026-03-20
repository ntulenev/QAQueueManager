namespace QAQueueManager.Abstractions;

/// <summary>
/// Coordinates the full QA queue workflow from data loading to export.
/// </summary>
internal interface IQaQueueApplication
{
    /// <summary>
    /// Runs the application workflow.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task RunAsync(CancellationToken cancellationToken);
}
