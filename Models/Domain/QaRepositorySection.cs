namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents one repository section in the report.
/// </summary>
/// <param name="Repository">The repository identity.</param>
/// <param name="WithoutTargetMerge">The issues without a target-branch merge.</param>
/// <param name="MergedIssueRows">The merged rows grouped by artifact version.</param>
internal sealed record QaRepositorySection(
    RepositoryRef Repository,
    IReadOnlyList<QaCodeIssueWithoutMerge> WithoutTargetMerge,
    IReadOnlyList<QaMergedIssueVersionRow> MergedIssueRows)
{
    /// <summary>
    /// Gets the full repository name.
    /// </summary>
    public RepositoryFullName RepositoryFullName => Repository.FullName;

    /// <summary>
    /// Gets the repository slug.
    /// </summary>
    public RepositorySlug RepositorySlug => Repository.Slug;
}
