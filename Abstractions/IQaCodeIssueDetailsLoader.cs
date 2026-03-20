using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Loads code-related details for Jira issues and resolves them into repository-specific results.
/// </summary>
internal interface IQaCodeIssueDetailsLoader
{
    /// <summary>
    /// Loads code-related details for the supplied Jira issues.
    /// </summary>
    /// <param name="issues">The Jira issues that have code links.</param>
    /// <param name="progress">The optional progress sink for interactive UI updates.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The processed issues with repository-specific code details.</returns>
    Task<IReadOnlyList<ProcessedCodeIssue>> LoadAsync(
        IReadOnlyList<QaIssue> issues,
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken);
}
