using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Maps Jira search metadata and issue DTOs into domain-friendly structures.
/// </summary>
internal interface IJiraIssueSearchMapper
{
    /// <summary>
    /// Maps Jira issue DTOs to domain issues.
    /// </summary>
    /// <param name="issues">The Jira issue DTOs.</param>
    /// <param name="developmentApiField">The Jira development field name.</param>
    /// <param name="teamApiFields">The Jira team field names.</param>
    /// <returns>The mapped domain issues.</returns>
    List<QaIssue> MapIssues(
        List<JiraIssueResponse> issues,
        string developmentApiField,
        IReadOnlyList<string> teamApiFields);
}
