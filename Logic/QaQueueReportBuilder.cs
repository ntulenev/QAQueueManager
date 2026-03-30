using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Builds the final QA queue report from processed Jira and Bitbucket data.
/// </summary>
internal sealed class QaQueueReportBuilder : IQaQueueReportBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueReportBuilder"/> class.
    /// </summary>
    /// <param name="jiraOptions">The Jira configuration options.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaQueueReportBuilder(
        IOptions<JiraOptions> jiraOptions,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraOptions);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _jiraOptions = jiraOptions.Value;
        _reportOptions = reportOptions.Value;
    }

    /// <inheritdoc />
    public QaQueueReport Build(
        IReadOnlyList<QaIssue> noCodeIssues,
        IReadOnlyList<ProcessedCodeIssue> processedIssues)
    {
        ArgumentNullException.ThrowIfNull(noCodeIssues);
        ArgumentNullException.ThrowIfNull(processedIssues);

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
                        .GetOrAdd(repositories, resolution.Repository)
                        .AddWithoutMerge(
                            processedIssue.Issue,
                            resolution.WithoutMerge.PullRequests,
                            resolution.WithoutMerge.BranchNames);
                    continue;
                }

                if (resolution.Merged is not null)
                {
                    RepositoryAccumulator
                        .GetOrAdd(repositories, resolution.Repository)
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
            repository.Repository,
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

    private readonly JiraOptions _jiraOptions;
    private readonly ReportOptions _reportOptions;
    private bool IsTeamGroupingEnabled => !string.IsNullOrWhiteSpace(_jiraOptions.TeamField);
}
