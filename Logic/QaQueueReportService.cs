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
    /// <param name="codeIssueDetailsLoader">The code issue details loader.</param>
    /// <param name="jiraOptions">The Jira configuration options.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaQueueReportService(
        IJiraIssueSearchClient jiraIssueSearchClient,
        IQaCodeIssueDetailsLoader codeIssueDetailsLoader,
        IOptions<JiraOptions> jiraOptions,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraIssueSearchClient);
        ArgumentNullException.ThrowIfNull(codeIssueDetailsLoader);
        ArgumentNullException.ThrowIfNull(jiraOptions);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _jiraIssueSearchClient = jiraIssueSearchClient;
        _codeIssueDetailsLoader = codeIssueDetailsLoader;
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
            .OrderBy(static issue => issue.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var processedIssues = await _codeIssueDetailsLoader
            .LoadAsync(codeIssues, progress, cancellationToken)
            .ConfigureAwait(false);

        var repositorySections = IsTeamGroupingEnabled
            ? []
            : BuildRepositorySections(processedIssues);
        var teamSections = IsTeamGroupingEnabled
            ? BuildTeamSections(noCodeIssues, processedIssues)
            : [];

        repositorySections = IsTeamGroupingEnabled
            ? []
            : ApplyDuplicateIssueAlerts(repositorySections);
        teamSections = IsTeamGroupingEnabled
            ? ApplyDuplicateIssueAlerts(teamSections)
            : [];

        return new QaQueueReport(
            DateTimeOffset.Now,
            _reportOptions.Title,
            _jiraOptions.Jql,
            new BranchName(_reportOptions.TargetBranch),
            IsTeamGroupingEnabled ? _jiraOptions.TeamField : null,
            _reportOptions.HideNoCodeIssues,
            noCodeIssues,
            repositorySections,
            teamSections);
    }

    private List<QaTeamSection> BuildTeamSections(
        IReadOnlyList<QaIssue> noCodeIssues,
        IEnumerable<ProcessedCodeIssue> processedIssues)
    {
        var noCodeByTeam = noCodeIssues
            .SelectMany(static issue => issue.GetTeamsOrFallback().Select(team => new { Team = team, Issue = issue }))
            .GroupBy(static item => item.Team.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<QaIssue>)[.. group
                    .Select(static item => item.Issue)
                    .Distinct()
                    .OrderBy(static issue => issue.Key.Value, StringComparer.OrdinalIgnoreCase)],
                StringComparer.OrdinalIgnoreCase);

        var processedByTeam = processedIssues
            .SelectMany(static issue => issue.Issue.GetTeamsOrFallback().Select(team => new { Team = team, Issue = issue }))
            .GroupBy(static item => item.Team.Value, StringComparer.OrdinalIgnoreCase)
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
        foreach (var teamName in teamNames
            .Select(static name => new TeamName(name))
            .OrderBy(static name => name, TeamNameComparer.Instance))
        {
            var teamNoCodeIssues = noCodeByTeam.TryGetValue(teamName.Value, out var issues)
                ? issues
                : [];
            var teamProcessedIssues = processedByTeam.TryGetValue(teamName.Value, out var processed)
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
                    RepositoryAccumulator
                        .GetOrAdd(repositories, resolution.RepositoryFullName, resolution.RepositorySlug)
                        .AddWithoutMerge(
                            processedIssue.Issue,
                            resolution.WithoutMerge.PullRequests,
                            resolution.WithoutMerge.BranchNames);
                    continue;
                }

                if (resolution.Merged is not null)
                {
                    RepositoryAccumulator
                        .GetOrAdd(repositories, resolution.RepositoryFullName, resolution.RepositorySlug)
                        .AddMerged(
                            processedIssue.Issue,
                            resolution.Merged.PullRequest,
                            resolution.Merged.Version);
                }
            }
        }

        return [.. repositories.Values
            .Select(static accumulator => accumulator.Build())
            .OrderBy(static section => section.RepositoryFullName.Value, StringComparer.OrdinalIgnoreCase)];
    }

    private static List<QaTeamSection> ApplyDuplicateIssueAlerts(IReadOnlyList<QaTeamSection> teamSections)
    {
        var occurrenceCounts = CountIssueOccurrences(teamSections.SelectMany(static team => team.Repositories));
        return [.. teamSections.Select(team => new QaTeamSection(
            team.Team,
            team.NoCodeIssues,
            ApplyDuplicateIssueAlerts(team.Repositories, occurrenceCounts)))];
    }

    private static List<QaRepositorySection> ApplyDuplicateIssueAlerts(IReadOnlyList<QaRepositorySection> repositorySections)
    {
        var occurrenceCounts = CountIssueOccurrences(repositorySections);
        return ApplyDuplicateIssueAlerts(repositorySections, occurrenceCounts);
    }

    private static List<QaRepositorySection> ApplyDuplicateIssueAlerts(
        IReadOnlyList<QaRepositorySection> repositorySections,
        IReadOnlyDictionary<JiraIssueId, int> occurrenceCounts)
    {
        return [.. repositorySections.Select(repository => new QaRepositorySection(
            repository.RepositoryFullName,
            repository.RepositorySlug,
            [.. repository.WithoutTargetMerge.Select(item => item with
            {
                HasDuplicateIssue = occurrenceCounts.GetValueOrDefault(item.Issue.Id) > 1
            })],
            [.. repository.MergedIssueRows.Select(item => item with
            {
                HasDuplicateIssue = occurrenceCounts.GetValueOrDefault(item.Issue.Id) > 1
            })]))];
    }

    private static Dictionary<JiraIssueId, int> CountIssueOccurrences(IEnumerable<QaRepositorySection> repositorySections)
    {
        var occurrenceCounts = new Dictionary<JiraIssueId, int>();

        foreach (var repository in repositorySections)
        {
            foreach (var item in repository.WithoutTargetMerge)
            {
                occurrenceCounts[item.Issue.Id] = occurrenceCounts.GetValueOrDefault(item.Issue.Id) + 1;
            }

            foreach (var item in repository.MergedIssueRows)
            {
                occurrenceCounts[item.Issue.Id] = occurrenceCounts.GetValueOrDefault(item.Issue.Id) + 1;
            }
        }

        return occurrenceCounts;
    }

    private readonly IJiraIssueSearchClient _jiraIssueSearchClient;
    private readonly IQaCodeIssueDetailsLoader _codeIssueDetailsLoader;
    private readonly JiraOptions _jiraOptions;
    private readonly ReportOptions _reportOptions;
    private bool IsTeamGroupingEnabled => !string.IsNullOrWhiteSpace(_jiraOptions.TeamField);
}
