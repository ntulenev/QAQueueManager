using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Builds repository-specific resolutions for one code-linked Jira issue.
/// </summary>
internal interface IRepositoryResolutionBuilder
{
    /// <summary>
    /// Builds repository-specific resolutions for the supplied Jira issue.
    /// </summary>
    /// <param name="issue">The Jira issue.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The repository resolutions for the issue.</returns>
    Task<IReadOnlyList<RepositoryResolution>> BuildAsync(QaIssue issue, CancellationToken cancellationToken);
}
