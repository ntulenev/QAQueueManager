using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using QAQueueManager.API;
using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

internal sealed class QaQueueReportService : IQaQueueReportService
{
    public QaQueueReportService(
        IJiraIssueSearchClient jiraIssueSearchClient,
        IJiraDevelopmentClient jiraDevelopmentClient,
        IBitbucketClient bitbucketClient,
        IOptions<JiraOptions> jiraOptions,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraIssueSearchClient);
        ArgumentNullException.ThrowIfNull(jiraDevelopmentClient);
        ArgumentNullException.ThrowIfNull(bitbucketClient);
        ArgumentNullException.ThrowIfNull(jiraOptions);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _jiraIssueSearchClient = jiraIssueSearchClient;
        _jiraDevelopmentClient = jiraDevelopmentClient;
        _bitbucketClient = bitbucketClient;
        _jiraOptions = jiraOptions.Value;
        _reportOptions = reportOptions.Value;
    }

    public async Task<QaQueueReport> BuildAsync(
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.JiraSearchStarted,
            "Loading issues from Jira"));

        var allIssues = await _jiraIssueSearchClient.SearchIssuesAsync(cancellationToken).ConfigureAwait(false);
        var codeIssues = allIssues
            .Where(static issue => issue.HasCode)
            .ToList();

        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.JiraSearchCompleted,
            $"Found {allIssues.Count} QA issues, {codeIssues.Count} with code",
            allIssues.Count,
            allIssues.Count));

        var noCodeIssues = allIssues
            .Where(static issue => !issue.HasCode)
            .OrderBy(static issue => issue.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var processedIssues = new ConcurrentBag<ProcessedCodeIssue>();
        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.CodeAnalysisStarted,
            codeIssues.Count == 0
                ? "No code-linked issues found"
                : $"Analyzing {codeIssues.Count} code-linked issues with max parallelism {_reportOptions.MaxParallelism}",
            0,
            codeIssues.Count));

        var startedCount = 0;
        var completedCount = 0;

        await Parallel.ForEachAsync(
            codeIssues,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _reportOptions.MaxParallelism
            },
            async (issue, ct) =>
            {
                var started = Interlocked.Increment(ref startedCount);
                progress?.Report(new QaQueueBuildProgress(
                    QaQueueBuildProgressKind.CodeIssueStarted,
                    $"Processing {issue.Key}",
                    started,
                    codeIssues.Count,
                    issue.Key));

                var result = await ProcessCodeIssueAsync(issue, ct).ConfigureAwait(false);
                processedIssues.Add(result);

                var completed = Interlocked.Increment(ref completedCount);
                progress?.Report(new QaQueueBuildProgress(
                    QaQueueBuildProgressKind.CodeIssueCompleted,
                    $"Processed {issue.Key}",
                    completed,
                    codeIssues.Count,
                    issue.Key));
            })
            .ConfigureAwait(false);

        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.CodeAnalysisCompleted,
            codeIssues.Count == 0
                ? "Code analysis skipped: no code-linked issues"
                : $"Processed {codeIssues.Count} code-linked issues",
            codeIssues.Count,
            codeIssues.Count));

        var repositorySections = IsTeamGroupingEnabled
            ? []
            : BuildRepositorySections(processedIssues);
        var teamSections = IsTeamGroupingEnabled
            ? BuildTeamSections(noCodeIssues, processedIssues)
            : [];

        return new QaQueueReport(
            DateTimeOffset.Now,
            _reportOptions.Title,
            _jiraOptions.Jql,
            _reportOptions.TargetBranch,
            IsTeamGroupingEnabled ? _jiraOptions.TeamField : null,
            _reportOptions.HideNoCodeIssues,
            noCodeIssues,
            repositorySections,
            teamSections);
    }

    private async Task<ProcessedCodeIssue> ProcessCodeIssueAsync(QaIssue issue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pullRequests = await _jiraDevelopmentClient.GetPullRequestsAsync(issue.Id, cancellationToken).ConfigureAwait(false);
        var branches = pullRequests.Count == 0
            ? await _jiraDevelopmentClient.GetBranchesAsync(issue.Id, cancellationToken).ConfigureAwait(false)
            : [];

        if (pullRequests.Count == 0 && branches.Count == 0)
        {
            return new ProcessedCodeIssue(
                issue,
                [new RepositoryResolution(
                    "Unknown repository",
                    "unknown",
                    new IssueWithoutMergeData([], []),
                    null)]);
        }

        var repositoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pullRequest in pullRequests)
        {
            _ = repositoryNames.Add(pullRequest.RepositoryFullName);
        }

        foreach (var branch in branches)
        {
            _ = repositoryNames.Add(branch.RepositoryFullName);
        }

        var resolutions = new List<RepositoryResolution>(repositoryNames.Count);
        foreach (var repositoryFullName in repositoryNames)
        {
            var repositorySlug = ExtractRepositorySlug(repositoryFullName);
            var repositoryPullRequests = pullRequests
                .Where(pr => string.Equals(pr.RepositoryFullName, repositoryFullName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var repositoryBranches = branches
                .Where(branch => string.Equals(branch.RepositoryFullName, repositoryFullName, StringComparison.OrdinalIgnoreCase))
                .Select(static branch => branch.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var candidate = repositoryPullRequests
                .Where(pr =>
                    string.Equals(pr.Status, "MERGED", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pr.DestinationBranch, _reportOptions.TargetBranch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static pr => pr.LastUpdatedOn ?? DateTimeOffset.MinValue)
                .ThenByDescending(static pr => pr.Id)
                .FirstOrDefault();

            if (candidate is null)
            {
                resolutions.Add(new RepositoryResolution(
                    repositoryFullName,
                    repositorySlug,
                    new IssueWithoutMergeData(repositoryPullRequests, repositoryBranches),
                    null));
                continue;
            }

            var bitbucketPullRequest = string.Equals(repositorySlug, "unknown", StringComparison.OrdinalIgnoreCase)
                ? null
                : await _bitbucketClient
                    .GetPullRequestAsync(repositorySlug, candidate.Id, cancellationToken)
                    .ConfigureAwait(false);

            if (bitbucketPullRequest is null)
            {
                resolutions.Add(new RepositoryResolution(
                    repositoryFullName,
                    repositorySlug,
                    null,
                    new MergedIssueData(
                        new BitbucketPullRequest(
                            candidate.Id,
                            candidate.Status,
                            repositoryFullName,
                            repositorySlug,
                            repositorySlug,
                            candidate.SourceBranch,
                            candidate.DestinationBranch,
                            candidate.Url,
                            string.Empty,
                            candidate.LastUpdatedOn),
                        VersionNotFound)));
                continue;
            }

            if (!string.Equals(bitbucketPullRequest.State, "MERGED", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(bitbucketPullRequest.DestinationBranch, _reportOptions.TargetBranch, StringComparison.OrdinalIgnoreCase))
            {
                resolutions.Add(new RepositoryResolution(
                    repositoryFullName,
                    repositorySlug,
                    new IssueWithoutMergeData(repositoryPullRequests, repositoryBranches),
                    null));
                continue;
            }

            var version = await ResolveVersionAsync(bitbucketPullRequest, cancellationToken).ConfigureAwait(false);
            resolutions.Add(new RepositoryResolution(
                repositoryFullName,
                repositorySlug,
                null,
                new MergedIssueData(bitbucketPullRequest, version)));
        }

        return new ProcessedCodeIssue(issue, resolutions);
    }

    private async Task<string> ResolveVersionAsync(
        BitbucketPullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pullRequest.MergeCommitHash))
        {
            return VersionNotFound;
        }

        var tags = await _bitbucketClient
            .GetTagsByCommitHashAsync(pullRequest.RepositorySlug, pullRequest.MergeCommitHash, cancellationToken)
            .ConfigureAwait(false);

        return tags.Count == 0 ? VersionNotFound : tags[0].Name;
    }

    private IReadOnlyList<QaTeamSection> BuildTeamSections(
        IReadOnlyList<QaIssue> noCodeIssues,
        IEnumerable<ProcessedCodeIssue> processedIssues)
    {
        var noCodeByTeam = noCodeIssues
            .SelectMany(static issue => GetIssueTeams(issue).Select(team => new { Team = team, Issue = issue }))
            .GroupBy(static item => item.Team, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<QaIssue>)[.. group
                    .Select(static item => item.Issue)
                    .Distinct()
                    .OrderBy(static issue => issue.Key, StringComparer.OrdinalIgnoreCase)],
                StringComparer.OrdinalIgnoreCase);

        var processedByTeam = processedIssues
            .SelectMany(static issue => GetIssueTeams(issue.Issue).Select(team => new { Team = team, Issue = issue }))
            .GroupBy(static item => item.Team, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .Select(static item => item.Issue)
                    .Distinct()
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var teamNames = new HashSet<string>(noCodeByTeam.Keys, StringComparer.OrdinalIgnoreCase);
        teamNames.UnionWith(processedByTeam.Keys);

        var result = new List<QaTeamSection>(teamNames.Count);
        foreach (var teamName in teamNames.OrderBy(static name => name, TeamNameComparer.Instance))
        {
            var teamNoCodeIssues = noCodeByTeam.TryGetValue(teamName, out var issues)
                ? issues
                : [];
            var teamProcessedIssues = processedByTeam.TryGetValue(teamName, out var processed)
                ? processed
                : [];
            var teamRepositories = BuildRepositorySections(teamProcessedIssues);

            if (_reportOptions.HideNoCodeIssues && teamRepositories.Count == 0)
            {
                continue;
            }

            if (teamNoCodeIssues.Count == 0 && teamRepositories.Count == 0)
            {
                continue;
            }

            result.Add(new QaTeamSection(teamName, teamNoCodeIssues, teamRepositories));
        }

        return result;
    }

    private static IReadOnlyList<QaRepositorySection> BuildRepositorySections(IEnumerable<ProcessedCodeIssue> processedIssues)
    {
        var repositories = new Dictionary<string, RepositoryAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var processedIssue in processedIssues)
        {
            foreach (var resolution in processedIssue.Resolutions)
            {
                if (resolution.WithoutMerge is not null)
                {
                    AddWithoutMerge(
                        repositories,
                        processedIssue.Issue,
                        resolution.RepositoryFullName,
                        resolution.RepositorySlug,
                        resolution.WithoutMerge.PullRequests,
                        resolution.WithoutMerge.BranchNames);
                    continue;
                }

                if (resolution.Merged is not null)
                {
                    AddMerged(
                        repositories,
                        processedIssue.Issue,
                        resolution.RepositoryFullName,
                        resolution.RepositorySlug,
                        resolution.Merged.PullRequest,
                        resolution.Merged.Version);
                }
            }
        }

        return repositories.Values
            .Select(static accumulator => accumulator.Build())
            .OrderBy(static section => section.RepositoryFullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddWithoutMerge(
        IDictionary<string, RepositoryAccumulator> repositories,
        QaIssue issue,
        string repositoryFullName,
        string repositorySlug,
        IReadOnlyList<JiraPullRequestLink> pullRequests,
        IReadOnlyList<string> branchNames)
    {
        var accumulator = GetAccumulator(repositories, repositoryFullName, repositorySlug);
        accumulator.WithoutTargetMerge.Add(new QaCodeIssueWithoutMerge(
            issue,
            repositoryFullName,
            repositorySlug,
            pullRequests,
            branchNames));
    }

    private static void AddMerged(
        IDictionary<string, RepositoryAccumulator> repositories,
        QaIssue issue,
        string repositoryFullName,
        string repositorySlug,
        BitbucketPullRequest pullRequest,
        string version)
    {
        var accumulator = GetAccumulator(repositories, repositoryFullName, repositorySlug);
        accumulator.MergedItems.Add(new PendingMergedIssue(
            issue,
            repositoryFullName,
            repositorySlug,
            new QaMergedPullRequest(
                pullRequest.Id,
                pullRequest.SourceBranch,
                pullRequest.DestinationBranch,
                version,
                pullRequest.HtmlUrl,
                pullRequest.MergeCommitHash,
                pullRequest.UpdatedOn)));
    }

    private static RepositoryAccumulator GetAccumulator(
        IDictionary<string, RepositoryAccumulator> repositories,
        string repositoryFullName,
        string repositorySlug)
    {
        if (repositories.TryGetValue(repositoryFullName, out var accumulator))
        {
            return accumulator;
        }

        accumulator = new RepositoryAccumulator(repositoryFullName, repositorySlug);
        repositories[repositoryFullName] = accumulator;
        return accumulator;
    }

    private static string ExtractRepositorySlug(string repositoryFullName)
    {
        if (string.IsNullOrWhiteSpace(repositoryFullName))
        {
            return "unknown";
        }

        var normalized = repositoryFullName.Trim().Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "unknown" : parts[^1];
    }

    private static string NormalizeTeamName(string? team) =>
        string.IsNullOrWhiteSpace(team) ? NoTeam : team.Trim();

    private static IReadOnlyList<string> GetIssueTeams(QaIssue issue)
    {
        var teams = issue.Teams
            .Select(NormalizeTeamName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return teams.Count == 0 ? [NoTeam] : teams;
    }

    private const string VersionNotFound = "Version not found";
    private const string NoTeam = "No team";
    private readonly IJiraIssueSearchClient _jiraIssueSearchClient;
    private readonly IJiraDevelopmentClient _jiraDevelopmentClient;
    private readonly IBitbucketClient _bitbucketClient;
    private readonly JiraOptions _jiraOptions;
    private readonly ReportOptions _reportOptions;
    private bool IsTeamGroupingEnabled => !string.IsNullOrWhiteSpace(_jiraOptions.TeamField);

    private sealed record ProcessedCodeIssue(
        QaIssue Issue,
        IReadOnlyList<RepositoryResolution> Resolutions);

    private sealed record RepositoryResolution(
        string RepositoryFullName,
        string RepositorySlug,
        IssueWithoutMergeData? WithoutMerge,
        MergedIssueData? Merged);

    private sealed record IssueWithoutMergeData(
        IReadOnlyList<JiraPullRequestLink> PullRequests,
        IReadOnlyList<string> BranchNames);

    private sealed record MergedIssueData(
        BitbucketPullRequest PullRequest,
        string Version);

    private sealed record PendingMergedIssue(
        QaIssue Issue,
        string RepositoryFullName,
        string RepositorySlug,
        QaMergedPullRequest PullRequest);

    private sealed class RepositoryAccumulator
    {
        public RepositoryAccumulator(string repositoryFullName, string repositorySlug)
        {
            RepositoryFullName = repositoryFullName;
            RepositorySlug = repositorySlug;
        }

        public string RepositoryFullName { get; }

        public string RepositorySlug { get; }

        public List<QaCodeIssueWithoutMerge> WithoutTargetMerge { get; } = [];

        public List<PendingMergedIssue> MergedItems { get; } = [];

        public QaRepositorySection Build()
        {
            var withoutMerge = WithoutTargetMerge
                .OrderBy(static item => item.Issue.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var mergedRows = MergedItems
                .GroupBy(static item => item.Issue.Id)
                .SelectMany(static group => BuildMergedIssueRows(group))
                .OrderBy(static item => item.Version, RepositoryVersionGroupComparer.Instance)
                .ThenBy(static item => item.Issue.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new QaRepositorySection(RepositoryFullName, RepositorySlug, withoutMerge, mergedRows);
        }

        private static IReadOnlyList<QaMergedIssueVersionRow> BuildMergedIssueRows(IGrouping<long, PendingMergedIssue> group)
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
                .Select(pr => NormalizeVersion(pr.Version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static version => version, RepositoryVersionGroupComparer.Instance)
                .ToList();
            var hasMultipleVersions = versions.Count > 1;

            return versions
                .Select(version => new QaMergedIssueVersionRow(
                    sample.Issue,
                    sample.RepositoryFullName,
                    sample.RepositorySlug,
                    version,
                    [.. pullRequests
                        .Where(pr => string.Equals(NormalizeVersion(pr.Version), version, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(static pr => pr.PullRequestUpdatedOn ?? DateTimeOffset.MinValue)
                        .ThenByDescending(static pr => pr.PullRequestId)],
                    hasMultipleVersions))
                .ToList();
        }

        private static string NormalizeVersion(string? version) =>
            string.IsNullOrWhiteSpace(version) ? QaQueueReportServiceVersionTokens.VersionNotFound : version.Trim();
    }
}

internal sealed class RepositoryVersionGroupComparer : IComparer<string>
{
    public static RepositoryVersionGroupComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (string.Equals(x, "Version not found", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(y, "Version not found", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        var compare = VersionNameComparer.Instance.Compare(x, y);
        return compare == 0 ? 0 : -compare;
    }
}

internal static class QaQueueReportServiceVersionTokens
{
    public const string VersionNotFound = "Version not found";
}

internal sealed class TeamNameComparer : IComparer<string>
{
    public static TeamNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (string.Equals(x, "No team", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(y, "No team", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
