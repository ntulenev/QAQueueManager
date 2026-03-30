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
                    issue.Key.Value));

                var result = await ProcessCodeIssueAsync(issue, ct).ConfigureAwait(false);
                processedIssues.Add(result);

                var completed = Interlocked.Increment(ref completedCount);
                progress?.Report(new QaQueueBuildProgress(
                    QaQueueBuildProgressKind.CodeIssueCompleted,
                    $"Processed {issue.Key}",
                    completed,
                    issues.Count,
                    issue.Key.Value));
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
        var developmentState = issue.DevelopmentState;

        if (developmentState.HasKnownNoDevelopment)
        {
            return new ProcessedCodeIssue(
                issue,
                [new RepositoryResolution(
                    RepositoryFullName.Unknown,
                    RepositorySlug.Unknown,
                    new IssueWithoutMergeData([], []),
                    null)]);
        }

        IReadOnlyList<JiraPullRequestLink> pullRequests;
        IReadOnlyList<JiraBranchLink> branches;

        if (developmentState.HasNoPullRequests)
        {
            pullRequests = [];
            branches = developmentState.HasNoBranches
                ? []
                : await _jiraDevelopmentClient.GetBranchesAsync(issue.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            pullRequests = await _jiraDevelopmentClient
                .GetPullRequestsAsync(issue.Id, cancellationToken)
                .ConfigureAwait(false);
            branches = pullRequests.Count == 0 && !developmentState.HasNoBranches
                ? await _jiraDevelopmentClient.GetBranchesAsync(issue.Id, cancellationToken).ConfigureAwait(false)
                : [];
        }

        if (pullRequests.Count == 0 && branches.Count == 0)
        {
            return new ProcessedCodeIssue(
                issue,
                [new RepositoryResolution(
                    RepositoryFullName.Unknown,
                    RepositorySlug.Unknown,
                    new IssueWithoutMergeData([], []),
                    null)]);
        }

        var repositoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pullRequest in pullRequests)
        {
            _ = repositoryNames.Add(pullRequest.RepositoryFullName.Value);
        }

        foreach (var branch in branches)
        {
            _ = repositoryNames.Add(branch.RepositoryFullName.Value);
        }

        var resolutions = new List<RepositoryResolution>(repositoryNames.Count);
        foreach (var repositoryName in repositoryNames)
        {
            var repositoryFullName = new RepositoryFullName(repositoryName);
            var repositorySlug = RepositorySlug.FromRepositoryFullName(repositoryFullName);
            var repositoryPullRequests = pullRequests
                .Where(pr => string.Equals(pr.RepositoryFullName.Value, repositoryFullName.Value, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var repositoryBranches = branches
                .Where(branch => string.Equals(branch.RepositoryFullName.Value, repositoryFullName.Value, StringComparison.OrdinalIgnoreCase))
                .Select(static branch => branch.Name)
                .GroupBy(static name => name.Value, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .OrderBy(static name => name.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var mergedCandidates = repositoryPullRequests
                .Where(pr =>
                    pr.Status.IsMerged &&
                    string.Equals(pr.DestinationBranch.Value, _reportOptions.TargetBranch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static pr => pr.LastUpdatedOn ?? DateTimeOffset.MinValue)
                .ThenByDescending(static pr => pr.Id)
                .GroupBy(static pr => pr.Id)
                .Select(static group => group.First())
                .ToList();

            if (mergedCandidates.Count == 0)
            {
                resolutions.Add(new RepositoryResolution(
                    repositoryFullName,
                    repositorySlug,
                    new IssueWithoutMergeData(repositoryPullRequests, repositoryBranches),
                    null));
                continue;
            }

            var mergedResolutions = new List<RepositoryResolution>(mergedCandidates.Count);
            foreach (var candidate in mergedCandidates)
            {
                var resolution = await TryBuildMergedResolutionAsync(
                    repositoryFullName,
                    repositorySlug,
                    candidate,
                    cancellationToken).ConfigureAwait(false);

                if (resolution is not null)
                {
                    mergedResolutions.Add(resolution);
                }
            }

            if (mergedResolutions.Count == 0)
            {
                resolutions.Add(new RepositoryResolution(
                    repositoryFullName,
                    repositorySlug,
                    new IssueWithoutMergeData(repositoryPullRequests, repositoryBranches),
                    null));
                continue;
            }

            resolutions.AddRange(mergedResolutions);
        }

        return new ProcessedCodeIssue(issue, resolutions);
    }

    private async Task<RepositoryResolution?> TryBuildMergedResolutionAsync(
        RepositoryFullName repositoryFullName,
        RepositorySlug repositorySlug,
        JiraPullRequestLink candidate,
        CancellationToken cancellationToken)
    {
        if (repositorySlug == RepositorySlug.Unknown)
        {
            return BuildMergedFallbackResolution(repositoryFullName, repositorySlug, candidate);
        }

        var bitbucketPullRequest = await _bitbucketClient
            .GetPullRequestAsync(repositorySlug, candidate.Id, cancellationToken)
            .ConfigureAwait(false);

        if (bitbucketPullRequest is null)
        {
            return BuildMergedFallbackResolution(repositoryFullName, repositorySlug, candidate);
        }

        if (!bitbucketPullRequest.State.IsMerged ||
            !string.Equals(bitbucketPullRequest.DestinationBranch.Value, _reportOptions.TargetBranch, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var version = await ResolveVersionAsync(bitbucketPullRequest, cancellationToken).ConfigureAwait(false);
        return new RepositoryResolution(
            repositoryFullName,
            repositorySlug,
            null,
            new MergedIssueData(bitbucketPullRequest, version));
    }

    private static RepositoryResolution BuildMergedFallbackResolution(
        RepositoryFullName repositoryFullName,
        RepositorySlug repositorySlug,
        JiraPullRequestLink candidate)
    {
        return new RepositoryResolution(
            repositoryFullName,
            repositorySlug,
            null,
            new MergedIssueData(
                new BitbucketPullRequest(
                    candidate.Id,
                    candidate.Status,
                    repositoryFullName,
                    new RepositoryDisplayName(repositorySlug.Value),
                    repositorySlug,
                    candidate.SourceBranch,
                    candidate.DestinationBranch,
                    candidate.Url,
                    null,
                    candidate.LastUpdatedOn),
                ArtifactVersion.NotFound));
    }

    private async Task<ArtifactVersion> ResolveVersionAsync(
        BitbucketPullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        if (pullRequest.MergeCommitHash is null)
        {
            return ArtifactVersion.NotFound;
        }

        var tags = await _bitbucketClient
            .GetTagsByCommitHashAsync(pullRequest.RepositorySlug, pullRequest.MergeCommitHash.Value, cancellationToken)
            .ConfigureAwait(false);

        return tags.Count == 0 ? ArtifactVersion.NotFound : tags[0].Name;
    }

    private readonly IJiraDevelopmentClient _jiraDevelopmentClient;
    private readonly IBitbucketClient _bitbucketClient;
    private readonly ReportOptions _reportOptions;
}
