using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Represents a processed code-linked issue with per-repository resolutions.
/// </summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="Resolutions">The repository resolutions for the issue.</param>
internal sealed record ProcessedCodeIssue(
    QaIssue Issue,
    IReadOnlyList<RepositoryResolution> Resolutions);
