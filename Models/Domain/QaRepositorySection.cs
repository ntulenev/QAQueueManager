namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents one repository section in the report.
/// </summary>
/// <param name="RepositoryFullName">The full repository name.</param>
/// <param name="RepositorySlug">The repository slug.</param>
/// <param name="WithoutTargetMerge">The issues without a target-branch merge.</param>
/// <param name="MergedIssueRows">The merged rows grouped by artifact version.</param>
internal sealed record QaRepositorySection(
    string RepositoryFullName,
    string RepositorySlug,
    IReadOnlyList<QaCodeIssueWithoutMerge> WithoutTargetMerge,
    IReadOnlyList<QaMergedIssueVersionRow> MergedIssueRows);
