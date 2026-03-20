using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Loads and resolves code details for Jira issues using Jira and Bitbucket metadata.
/// </summary>
internal sealed class QaCodeIssueDetailsLoader : IQaCodeIssueDetailsLoader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaCodeIssueDetailsLoader"/> class.
    /// </summary>
    /// <param name="jiraDevelopmentClient">The Jira development client.</param>
    /// <param name="bitbucketClient">The Bitbucket client.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaCodeIssueDetailsLoader(
        IJiraDevelopmentClient jiraDevelopmentClient,
        IBitbucketClient bitbucketClient,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraDevelopmentClient);
        ArgumentNullException.ThrowIfNull(bitbucketClient);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _jiraDevelopmentClient = jiraDevelopmentClient;
        _bitbucketClient = bitbucketClient;
        _reportOptions = reportOptions.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessedCodeIssue>> LoadAsync(
        IReadOnlyList<QaIssue> issues,
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var processedIssues = new ConcurrentBag<ProcessedCodeIssue>();
        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.CodeAnalysisStarted,
            issues.Count == 0
                ? "No code-linked issues found"
                : $"Analyzing {issues.Count} code-linked issues with max parallelism {_reportOptions.MaxParallelism}",
            0,
            issues.Count));

        var startedCount = 0;
        var completedCount = 0;

        await Parallel.ForEachAsync(
            issues,
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
                    issues.Count,
                    issue.Key));

                var result = await ProcessCodeIssueAsync(issue, ct).ConfigureAwait(false);
                processedIssues.Add(result);

                var completed = Interlocked.Increment(ref completedCount);
                progress?.Report(new QaQueueBuildProgress(
                    QaQueueBuildProgressKind.CodeIssueCompleted,
                    $"Processed {issue.Key}",
                    completed,
                    issues.Count,
                    issue.Key));
            })
            .ConfigureAwait(false);

        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.CodeAnalysisCompleted,
            issues.Count == 0
                ? "Code analysis skipped: no code-linked issues"
                : $"Processed {issues.Count} code-linked issues",
            issues.Count,
            issues.Count));

        return [.. processedIssues];
    }

    private async Task<ProcessedCodeIssue> ProcessCodeIssueAsync(
        QaIssue issue,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pullRequests = await _jiraDevelopmentClient
            .GetPullRequestsAsync(issue.Id, cancellationToken)
            .ConfigureAwait(false);
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
                        QaQueueReportServiceVersionTokens.VERSION_NOT_FOUND)));
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
            return QaQueueReportServiceVersionTokens.VERSION_NOT_FOUND;
        }

        var tags = await _bitbucketClient
            .GetTagsByCommitHashAsync(pullRequest.RepositorySlug, pullRequest.MergeCommitHash.Value, cancellationToken)
            .ConfigureAwait(false);

        return tags.Count == 0 ? QaQueueReportServiceVersionTokens.VERSION_NOT_FOUND : tags[0].Name;
    }

    private readonly IJiraDevelopmentClient _jiraDevelopmentClient;
    private readonly IBitbucketClient _bitbucketClient;
    private readonly ReportOptions _reportOptions;
}
