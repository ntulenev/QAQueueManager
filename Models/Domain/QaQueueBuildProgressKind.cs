namespace QAQueueManager.Models.Domain;

/// <summary>
/// Defines progress event kinds emitted during report building.
/// </summary>
internal enum QaQueueBuildProgressKind
{
    /// <summary>
    /// Jira search has started.
    /// </summary>
    JiraSearchStarted,

    /// <summary>
    /// Jira search has completed.
    /// </summary>
    JiraSearchCompleted,

    /// <summary>
    /// Code analysis has started.
    /// </summary>
    CodeAnalysisStarted,

    /// <summary>
    /// Processing of a code-linked issue has started.
    /// </summary>
    CodeIssueStarted,

    /// <summary>
    /// Processing of a code-linked issue has completed.
    /// </summary>
    CodeIssueCompleted,

    /// <summary>
    /// Code analysis has completed.
    /// </summary>
    CodeAnalysisCompleted
}
