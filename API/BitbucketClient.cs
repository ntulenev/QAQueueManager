using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

/// <summary>
/// Loads and normalizes Bitbucket data used by the report builder.
/// </summary>
internal sealed class BitbucketClient : IBitbucketClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketClient"/> class.
    /// </summary>
    /// <param name="transport">The Bitbucket transport.</param>
    /// <param name="options">The Bitbucket configuration options.</param>
    public BitbucketClient(BitbucketTransport transport, IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);

        _transport = transport;
        _options = options.Value;
    }

    /// <summary>
    /// Loads a Bitbucket pull request by repository slug and id.
    /// </summary>
    /// <param name="repositorySlug">The repository slug.</param>
    /// <param name="pullRequestId">The pull request identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mapped pull request, or <see langword="null"/> when it cannot be loaded.</returns>
    public async Task<BitbucketPullRequest?> GetPullRequestAsync(
        RepositorySlug repositorySlug,
        PullRequestId pullRequestId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{repositorySlug}#{pullRequestId}";
        if (_pullRequestCache.TryGetValue(cacheKey, out var cachedPullRequest))
        {
            return cachedPullRequest;
        }

        var url = new Uri(
            $"repositories/{_options.Workspace}/{Uri.EscapeDataString(repositorySlug.Value)}/pullrequests/{pullRequestId.Value}",
            UriKind.Relative);

        BitbucketPullRequestResponse? response;
        try
        {
            response = await _transport
                .GetAsync<BitbucketPullRequestResponse>(url, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            _pullRequestCache[cacheKey] = null;
            return null;
        }

        if (response is null)
        {
            return null;
        }

        var repositoryFullName = response.Destination?.Repository?.FullName?.Trim();
        var repositoryDisplayName = response.Destination?.Repository?.Name?.Trim();
        var htmlUrl = response.Links?.Html?.Href?.Trim() ?? string.Empty;
        var mergeCommitHash = CommitHash.TryCreate(response.MergeCommit?.Hash, out var parsedMergeCommitHash)
            ? (CommitHash?)parsedMergeCommitHash
            : null;

        var mapped = new BitbucketPullRequest(
            new PullRequestId(response.Id),
            response.State?.Trim() ?? "UNKNOWN",
            string.IsNullOrWhiteSpace(repositoryFullName)
                ? $"{_options.Workspace}/{repositorySlug.Value}"
                : repositoryFullName,
            string.IsNullOrWhiteSpace(repositoryDisplayName) ? repositorySlug.Value : repositoryDisplayName,
            repositorySlug,
            response.Source?.Branch?.Name?.Trim() ?? "-",
            response.Destination?.Branch?.Name?.Trim() ?? "-",
            htmlUrl,
            mergeCommitHash,
            response.UpdatedOn);

        _pullRequestCache[cacheKey] = mapped;
        return mapped;
    }

    /// <summary>
    /// Loads repository tags that point to the specified commit hash.
    /// </summary>
    /// <param name="repositorySlug">The repository slug.</param>
    /// <param name="commitHash">The commit hash to match.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The matching tags ordered by version semantics.</returns>
    public async Task<IReadOnlyList<BitbucketTag>> GetTagsByCommitHashAsync(
        RepositorySlug repositorySlug,
        CommitHash commitHash,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{repositorySlug}@{commitHash}";
        if (_tagCache.TryGetValue(cacheKey, out var cachedTags))
        {
            return cachedTags;
        }

        var allTags = await GetRepositoryTagsAsync(repositorySlug, cancellationToken).ConfigureAwait(false);
        var tags = allTags
            .Where(tag => IsMatchingCommitHash(tag.TargetHash, commitHash))
            .OrderBy(static tag => tag.Name, VersionNameComparer.Instance)
            .ToList();

        _tagCache[cacheKey] = tags;
        return tags;
    }

    private async Task<IReadOnlyList<BitbucketTag>> GetRepositoryTagsAsync(
        RepositorySlug repositorySlug,
        CancellationToken cancellationToken)
    {
        if (_repositoryTagCache.TryGetValue(repositorySlug, out var cachedTags))
        {
            return cachedTags;
        }

        var tags = new List<BitbucketTag>();
        Uri? next = new Uri(
            $"repositories/{_options.Workspace}/{Uri.EscapeDataString(repositorySlug.Value)}/refs/tags?pagelen=100",
            UriKind.Relative);

        while (next is not null)
        {
            BitbucketTagPageResponse? response;
            try
            {
                response = await _transport
                    .GetAsync<BitbucketTagPageResponse>(next, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                _repositoryTagCache[repositorySlug] = [];
                return _repositoryTagCache[repositorySlug];
            }

            if (response?.Values is not null)
            {
                tags.AddRange(
                    response.Values
                        .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
                        .Select(static item => new BitbucketTag(
                            item.Name!.Trim(),
                            CommitHash.TryCreate(item.Target?.Hash, out var hash) ? hash : null,
                            item.Date)));
            }

            next = CreateNextUri(response?.Next);
        }

        var distinctTags = tags
            .GroupBy(static tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static tag => tag.Name, VersionNameComparer.Instance)
            .ToList();

        _repositoryTagCache[repositorySlug] = distinctTags;
        return distinctTags;
    }

    private static Uri? CreateNextUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        return Uri.TryCreate(value, UriKind.Relative, out var relative)
            ? relative
            : null;
    }

    private static bool IsMatchingCommitHash(CommitHash? tagHash, CommitHash commitHash)
    {
        if (tagHash is null)
        {
            return false;
        }

        var normalizedTagHash = tagHash.Value.Value;
        var normalizedCommitHash = commitHash.Value;

        return normalizedTagHash.Equals(normalizedCommitHash, StringComparison.OrdinalIgnoreCase)
            || normalizedTagHash.StartsWith(normalizedCommitHash, StringComparison.OrdinalIgnoreCase)
            || normalizedCommitHash.StartsWith(normalizedTagHash, StringComparison.OrdinalIgnoreCase);
    }

    private readonly BitbucketTransport _transport;
    private readonly BitbucketOptions _options;
    private readonly ConcurrentDictionary<string, BitbucketPullRequest?> _pullRequestCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<BitbucketTag>> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<RepositorySlug, IReadOnlyList<BitbucketTag>> _repositoryTagCache = [];
}
