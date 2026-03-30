namespace QAQueueManager.Models.Domain;

/// <summary>
/// Accumulates per-repository data before materializing the final repository section.
/// </summary>
internal sealed class RepositoryAccumulator
{
    /// <summary>
    /// Gets or creates a repository accumulator for the supplied repository.
    /// </summary>
    /// <param name="repositories">The accumulator dictionary keyed by repository full name.</param>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="repositorySlug">The repository slug.</param>
    /// <returns>The matching repository accumulator.</returns>
    public static RepositoryAccumulator GetOrAdd(
        IDictionary<string, RepositoryAccumulator> repositories,
        RepositoryFullName repositoryFullName,
        RepositorySlug repositorySlug)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var repositoryKey = repositoryFullName.Value;

        if (repositories.TryGetValue(repositoryKey, out var accumulator))
        {
            return accumulator;
        }

        accumulator = new RepositoryAccumulator(repositoryFullName, repositorySlug);
        repositories[repositoryKey] = accumulator;
        return accumulator;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryAccumulator"/> class.
    /// </summary>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <param name="repositorySlug">The repository slug.</param>
    public RepositoryAccumulator(RepositoryFullName repositoryFullName, RepositorySlug repositorySlug)
    {
        RepositoryFullName = repositoryFullName;
        RepositorySlug = repositorySlug;
    }

    /// <summary>
    /// Gets the full repository name.
    /// </summary>
    public RepositoryFullName RepositoryFullName { get; }

    /// <summary>
    /// Gets the repository slug.
    /// </summary>
    public RepositorySlug RepositorySlug { get; }

    /// <summary>
    /// Gets the issues without a target-branch merge.
    /// </summary>
    public List<QaCodeIssueWithoutMerge> WithoutTargetMerge { get; } = [];

    /// <summary>
    /// Gets the intermediate merged issue items.
    /// </summary>
    public List<PendingMergedIssue> MergedItems { get; } = [];

    /// <summary>
    /// Adds a no-merge issue entry to the accumulator.
    /// </summary>
    /// <param name="issue">The Jira issue.</param>
    /// <param name="pullRequests">The related pull requests.</param>
    /// <param name="branchNames">The related branch names.</param>
    public void AddWithoutMerge(
        QaIssue issue,
        IReadOnlyList<JiraPullRequestLink> pullRequests,
        IReadOnlyList<BranchName> branchNames)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(pullRequests);
        ArgumentNullException.ThrowIfNull(branchNames);

        WithoutTargetMerge.Add(new QaCodeIssueWithoutMerge(
            issue,
            RepositoryFullName,
            RepositorySlug,
            pullRequests,
            branchNames,
            HasDuplicateIssue: false));
    }

    /// <summary>
    /// Adds a merged issue entry to the accumulator.
    /// </summary>
    /// <param name="issue">The Jira issue.</param>
    /// <param name="pullRequest">The normalized Bitbucket pull request.</param>
    /// <param name="version">The resolved artifact version.</param>
    public void AddMerged(QaIssue issue, BitbucketPullRequest pullRequest, ArtifactVersion version)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(pullRequest);

        MergedItems.Add(PendingMergedIssue.Create(
            issue,
            RepositoryFullName,
            RepositorySlug,
            pullRequest,
            version));
    }

    /// <summary>
    /// Builds the final repository section from the accumulated items.
    /// </summary>
    /// <returns>The repository section.</returns>
    public QaRepositorySection Build()
    {
        var withoutMerge = WithoutTargetMerge
            .OrderBy(static item => item.Issue.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mergedRows = MergedItems
            .GroupBy(static item => item.Issue.Id)
            .SelectMany(static group => BuildMergedIssueRows(group))
            .OrderBy(static item => item.Version, RepositoryVersionGroupComparer.Instance)
            .ThenBy(static item => item.Issue.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new QaRepositorySection(RepositoryFullName, RepositorySlug, withoutMerge, mergedRows);
    }

    private static List<QaMergedIssueVersionRow> BuildMergedIssueRows(IGrouping<JiraIssueId, PendingMergedIssue> group)
    {
        var items = group.ToList();
        var sample = items[0];
        var pullRequests = items
            .Select(static item => item.PullRequest)
            .GroupBy(static pr => pr.PullRequestId)
            .Select(static prGroup => prGroup.First())
            .OrderByDescending(static pr => pr.PullRequestUpdatedOn ?? DateTimeOffset.MinValue)
            .ThenByDescending(static pr => pr.PullRequestId)
            .ToList();

        var versions = pullRequests
            .Select(static pr => pr.Version)
            .GroupBy(static version => version.Value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static version => version, RepositoryVersionGroupComparer.Instance)
            .ToList();

        return [.. versions
            .Select(version => new QaMergedIssueVersionRow(
                sample.Issue,
                sample.RepositoryFullName,
                sample.RepositorySlug,
                version,
                [.. pullRequests
                    .Where(pr => pr.Version == version)
                    .OrderByDescending(static pr => pr.PullRequestUpdatedOn ?? DateTimeOffset.MinValue)
                    .ThenByDescending(static pr => pr.PullRequestId)],
                HasDuplicateIssue: false))];
    }
}
