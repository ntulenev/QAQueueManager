using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Executes the QA queue build and export workflow.
/// </summary>
internal interface IQaQueueWorkflowRunner
{
    /// <summary>
    /// Builds the report and exports all configured outputs.
    /// </summary>
    /// <param name="progress">The workflow progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The generated report and exported file paths.</returns>
    Task<QaQueueWorkflowResult> RunAsync(
        IQaQueueWorkflowProgress progress,
        CancellationToken cancellationToken);
}
