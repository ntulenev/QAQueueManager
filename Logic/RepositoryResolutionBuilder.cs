using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Builds repository-specific merged and non-merged resolutions for one Jira issue.
/// </summary>
internal sealed class RepositoryResolutionBuilder : IRepositoryResolutionBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryResolutionBuilder"/> class.
    /// </summary>
    /// <param name="jiraDevelopmentClient">The Jira development client.</param>
    /// <param name="bitbucketClient">The Bitbucket client.</param>
    /// <param name="artifactVersionResolver">The artifact version resolver.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public RepositoryResolutionBuilder(
        IJiraDevelopmentClient jiraDevelopmentClient,
        IBitbucketClient bitbucketClient,
        IArtifactVersionResolver artifactVersionResolver,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(jiraDevelopmentClient);
        ArgumentNullException.ThrowIfNull(bitbucketClient);
        ArgumentNullException.ThrowIfNull(artifactVersionResolver);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _jiraDevelopmentClient = jiraDevelopmentClient;
        _bitbucketClient = bitbucketClient;
        _artifactVersionResolver = artifactVersionResolver;
        _reportOptions = reportOptions.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepositoryResolution>> BuildAsync(
        QaIssue issue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);

        cancellationToken.ThrowIfCancellationRequested();
        var developmentState = issue.DevelopmentState;

        if (developmentState.HasKnownNoDevelopment)
        {
            return [RepositoryResolution.CreateUnknownWithoutMerge()];
        }

        var developmentLinks = await LoadDevelopmentLinksAsync(issue, developmentState, cancellationToken).ConfigureAwait(false);
        if (developmentLinks.PullRequests.Count == 0 && developmentLinks.Branches.Count == 0)
        {
            return [RepositoryResolution.CreateUnknownWithoutMerge()];
        }

        var repositoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pullRequest in developmentLinks.PullRequests)
        {
            _ = repositoryNames.Add(pullRequest.RepositoryFullName.Value);
        }

        foreach (var branch in developmentLinks.Branches)
        {
            _ = repositoryNames.Add(branch.RepositoryFullName.Value);
        }

        var resolutions = new List<RepositoryResolution>(repositoryNames.Count);
        foreach (var repositoryName in repositoryNames)
        {
            var repositoryFullName = new RepositoryFullName(repositoryName);
            var repository = RepositoryRef.FromFullName(repositoryFullName);
            var repositoryPullRequests = developmentLinks.PullRequests
                .Where(pr => string.Equals(pr.RepositoryFullName.Value, repositoryFullName.Value, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var repositoryBranches = developmentLinks.Branches
                .Where(branch => string.Equals(branch.RepositoryFullName.Value, repositoryFullName.Value, StringComparison.OrdinalIgnoreCase))
                .Select(static branch => branch.Name)
                .GroupBy(static name => name.Value, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .OrderBy(static name => name.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var mergedCandidates = repositoryPullRequests
                .Where(pr => pr.IsMergedInto(TargetBranch))
                .OrderByDescending(static pr => pr.LastUpdatedOn ?? DateTimeOffset.MinValue)
                .ThenByDescending(static pr => pr.Id)
                .GroupBy(static pr => pr.Id)
                .Select(static group => group.First())
                .ToList();

            if (mergedCandidates.Count == 0)
            {
                resolutions.Add(RepositoryResolution.CreateWithoutMerge(
                    repository,
                    repositoryPullRequests,
                    repositoryBranches));
                continue;
            }

            var mergedResolutions = new List<RepositoryResolution>(mergedCandidates.Count);
            foreach (var candidate in mergedCandidates)
            {
                var resolution = await TryBuildMergedResolutionAsync(
                    repositoryFullName,
                    repository,
                    candidate,
                    cancellationToken).ConfigureAwait(false);

                if (resolution is not null)
                {
                    mergedResolutions.Add(resolution);
                }
            }

            if (mergedResolutions.Count == 0)
            {
                resolutions.Add(RepositoryResolution.CreateWithoutMerge(
                    repository,
                    repositoryPullRequests,
                    repositoryBranches));
                continue;
            }

            resolutions.AddRange(mergedResolutions);
        }

        return resolutions;
    }

    private async Task<IssueDevelopmentLinks> LoadDevelopmentLinksAsync(
        QaIssue issue,
        QaIssueDevelopmentState developmentState,
        CancellationToken cancellationToken)
    {
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

        return new IssueDevelopmentLinks(pullRequests, branches);
    }

    private async Task<RepositoryResolution?> TryBuildMergedResolutionAsync(
        RepositoryFullName repositoryFullName,
        RepositoryRef repository,
        JiraPullRequestLink candidate,
        CancellationToken cancellationToken)
    {
        if (repository.Slug == RepositorySlug.Unknown)
        {
            return RepositoryResolution.CreateMergedFallback(repositoryFullName, repository, candidate);
        }

        var bitbucketPullRequest = await _bitbucketClient
            .GetPullRequestAsync(repository.Slug, candidate.Id, cancellationToken)
            .ConfigureAwait(false);

        if (bitbucketPullRequest is null)
        {
            return RepositoryResolution.CreateMergedFallback(repositoryFullName, repository, candidate);
        }

        if (!bitbucketPullRequest.IsMergedInto(TargetBranch))
        {
            return null;
        }

        var version = await _artifactVersionResolver.ResolveAsync(bitbucketPullRequest, cancellationToken).ConfigureAwait(false);
        return RepositoryResolution.CreateMerged(repository, bitbucketPullRequest, version);
    }

    private readonly IJiraDevelopmentClient _jiraDevelopmentClient;
    private readonly IBitbucketClient _bitbucketClient;
    private readonly IArtifactVersionResolver _artifactVersionResolver;
    private readonly ReportOptions _reportOptions;
    private BranchName TargetBranch => new(_reportOptions.TargetBranch);

    private sealed record IssueDevelopmentLinks(
        IReadOnlyList<JiraPullRequestLink> PullRequests,
        IReadOnlyList<JiraBranchLink> Branches);
}
