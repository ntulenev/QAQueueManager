using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Builds the QA queue report from Jira issues and Bitbucket metadata.
/// </summary>
internal sealed class QaQueueReportService : IQaQueueReportService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueReportService"/> class.
    /// </summary>
    /// <param name="jiraIssueSearchClient">The Jira issue search client.</param>
    /// <param name="jiraDevelopmentClient">The Jira development client.</param>
    /// <param name="bitbucketClient">The Bitbucket client.</param>
    /// <param name="jiraOptions">The Jira configuration options.</param>
    /// <param name="reportOptions">The report configuration options.</param>
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

    /// <summary>
    /// Builds the QA queue report and emits optional progress updates.
    /// </summary>
    /// <param name="progress">The optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The generated QA queue report.</returns>
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
                    RepositorySlug.Unknown,
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
            var repositorySlug = RepositorySlug.FromRepositoryFullName(repositoryFullName);
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

            var bitbucketPullRequest = repositorySlug == RepositorySlug.Unknown
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
                            repositorySlug.Value,
                            repositorySlug,
                            candidate.SourceBranch,
                            candidate.DestinationBranch,
                            candidate.Url,
                            null,
                            candidate.LastUpdatedOn),
                        VERSION_NOT_FOUND)));
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
        if (pullRequest.MergeCommitHash is null)
        {
            return VERSION_NOT_FOUND;
        }

        var tags = await _bitbucketClient
            .GetTagsByCommitHashAsync(pullRequest.RepositorySlug, pullRequest.MergeCommitHash.Value, cancellationToken)
            .ConfigureAwait(false);

        return tags.Count == 0 ? VERSION_NOT_FOUND : tags[0].Name;
    }

    private List<QaTeamSection> BuildTeamSections(
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

    private static List<QaRepositorySection> BuildRepositorySections(IEnumerable<ProcessedCodeIssue> processedIssues)
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

        return [.. repositories.Values
            .Select(static accumulator => accumulator.Build())
            .OrderBy(static section => section.RepositoryFullName, StringComparer.OrdinalIgnoreCase)];
    }

    private static void AddWithoutMerge(
        IDictionary<string, RepositoryAccumulator> repositories,
        QaIssue issue,
        string repositoryFullName,
        RepositorySlug repositorySlug,
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
        RepositorySlug repositorySlug,
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
        RepositorySlug repositorySlug)
    {
        if (repositories.TryGetValue(repositoryFullName, out var accumulator))
        {
            return accumulator;
        }

        accumulator = new RepositoryAccumulator(repositoryFullName, repositorySlug);
        repositories[repositoryFullName] = accumulator;
        return accumulator;
    }

    private static List<string> GetIssueTeams(QaIssue issue)
    {
        var teams = issue.GetNormalizedTeams();

        return teams.Count == 0 ? [NO_TEAM] : [.. teams];
    }

    private const string VERSION_NOT_FOUND = "Version not found";
    private const string NO_TEAM = "No team";
    private readonly IJiraIssueSearchClient _jiraIssueSearchClient;
    private readonly IJiraDevelopmentClient _jiraDevelopmentClient;
    private readonly IBitbucketClient _bitbucketClient;
    private readonly JiraOptions _jiraOptions;
    private readonly ReportOptions _reportOptions;
    private bool IsTeamGroupingEnabled => !string.IsNullOrWhiteSpace(_jiraOptions.TeamField);
}
