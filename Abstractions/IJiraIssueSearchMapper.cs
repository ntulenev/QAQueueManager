using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Maps Jira search metadata and issue DTOs into domain-friendly structures.
/// </summary>
internal interface IJiraIssueSearchMapper
{
    /// <summary>
    /// Normalizes a configured Jira field alias for lookup.
    /// </summary>
    /// <param name="alias">The configured field alias.</param>
    /// <returns>The normalized lookup key.</returns>
    string SimplifyAlias(string alias);

    /// <summary>
    /// Builds a field alias lookup from Jira field definitions.
    /// </summary>
    /// <param name="fields">The Jira field definitions.</param>
    /// <returns>The alias lookup keyed by normalized field names.</returns>
    Dictionary<string, IReadOnlyList<string>> BuildFieldLookup(IEnumerable<JiraFieldDefinitionResponse> fields);

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
