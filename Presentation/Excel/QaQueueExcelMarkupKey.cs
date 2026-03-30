using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Represents a typed markup key used to correlate Excel rows across reports.
/// </summary>
/// <param name="Value">The serialized markup key value.</param>
internal sealed record QaQueueExcelMarkupKey(string Value)
{
    private const string NO_CODE_SERVICE_KEY = "__no-code__";
    private const char SEPARATOR = '|';

    /// <summary>
    /// Creates a markup key for a no-code issue row.
    /// </summary>
    /// <param name="sheetName">The worksheet name.</param>
    /// <param name="issueKey">The Jira issue key.</param>
    /// <returns>The typed markup key.</returns>
    internal static QaQueueExcelMarkupKey CreateNoCode(ExcelSheetName sheetName, JiraIssueKey issueKey) =>
        new(string.Join(SEPARATOR, sheetName.Value, NO_CODE_SERVICE_KEY, issueKey.Value));

    /// <summary>
    /// Creates a markup key for a repository issue row without a target-branch merge.
    /// </summary>
    /// <param name="sheetName">The worksheet name.</param>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="issueKey">The Jira issue key.</param>
    /// <returns>The typed markup key.</returns>
    internal static QaQueueExcelMarkupKey CreateWithoutMerge(
        ExcelSheetName sheetName,
        RepositoryFullName repositoryFullName,
        JiraIssueKey issueKey) =>
        new(string.Join(SEPARATOR, sheetName.Value, repositoryFullName.Value, issueKey.Value));

    /// <summary>
    /// Creates a markup key for a merged repository issue row.
    /// </summary>
    /// <param name="sheetName">The worksheet name.</param>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="issueKey">The Jira issue key.</param>
    /// <param name="version">The artifact version.</param>
    /// <returns>The typed markup key.</returns>
    internal static QaQueueExcelMarkupKey CreateMerged(
        ExcelSheetName sheetName,
        RepositoryFullName repositoryFullName,
        JiraIssueKey issueKey,
        ArtifactVersion version) =>
        new(string.Join(SEPARATOR, sheetName.Value, repositoryFullName.Value, issueKey.Value, version.Value));
}
