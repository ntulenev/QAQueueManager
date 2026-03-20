namespace QAQueueManager.Abstractions;

/// <summary>
/// Hosts workflow progress presentation while the QA queue application runs.
/// </summary>
internal interface IQaQueueWorkflowProgressHost
{
    /// <summary>
    /// Runs the supplied workflow within the configured progress presentation.
    /// </summary>
    /// <param name="runAsync">The workflow to execute with progress reporting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RunAsync(Func<IQaQueueWorkflowProgress, Task> runAsync);
}
