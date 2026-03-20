using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

internal sealed class BitbucketClient : IBitbucketClient
{
    public BitbucketClient(BitbucketTransport transport, IOptions<BitbucketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);

        _transport = transport;
        _options = options.Value;
    }

    public async Task<BitbucketPullRequest?> GetPullRequestAsync(
        string repositorySlug,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{repositorySlug}#{pullRequestId}";
        if (_pullRequestCache.TryGetValue(cacheKey, out var cachedPullRequest))
        {
            return cachedPullRequest;
        }

        var url = new Uri(
            $"repositories/{_options.Workspace}/{Uri.EscapeDataString(repositorySlug)}/pullrequests/{pullRequestId}",
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
        var mergeCommitHash = response.MergeCommit?.Hash?.Trim() ?? string.Empty;

        var mapped = new BitbucketPullRequest(
            response.Id,
            response.State?.Trim() ?? "UNKNOWN",
            string.IsNullOrWhiteSpace(repositoryFullName)
                ? $"{_options.Workspace}/{repositorySlug}"
                : repositoryFullName,
            string.IsNullOrWhiteSpace(repositoryDisplayName) ? repositorySlug : repositoryDisplayName,
            repositorySlug,
            response.Source?.Branch?.Name?.Trim() ?? "-",
            response.Destination?.Branch?.Name?.Trim() ?? "-",
            htmlUrl,
            mergeCommitHash,
            response.UpdatedOn);

        _pullRequestCache[cacheKey] = mapped;
        return mapped;
    }

    public async Task<IReadOnlyList<BitbucketTag>> GetTagsByCommitHashAsync(
        string repositorySlug,
        string commitHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositorySlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitHash);

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
        string repositorySlug,
        CancellationToken cancellationToken)
    {
        if (_repositoryTagCache.TryGetValue(repositorySlug, out var cachedTags))
        {
            return cachedTags;
        }

        var tags = new List<BitbucketTag>();
        Uri? next = new Uri(
            $"repositories/{_options.Workspace}/{Uri.EscapeDataString(repositorySlug)}/refs/tags?pagelen=100",
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
                            item.Target?.Hash?.Trim() ?? string.Empty,
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

    private static bool IsMatchingCommitHash(string? tagHash, string? commitHash)
    {
        if (string.IsNullOrWhiteSpace(tagHash) || string.IsNullOrWhiteSpace(commitHash))
        {
            return false;
        }

        var normalizedTagHash = tagHash.Trim();
        var normalizedCommitHash = commitHash.Trim();

        return normalizedTagHash.Equals(normalizedCommitHash, StringComparison.OrdinalIgnoreCase)
            || normalizedTagHash.StartsWith(normalizedCommitHash, StringComparison.OrdinalIgnoreCase)
            || normalizedCommitHash.StartsWith(normalizedTagHash, StringComparison.OrdinalIgnoreCase);
    }

    private readonly BitbucketTransport _transport;
    private readonly BitbucketOptions _options;
    private readonly ConcurrentDictionary<string, BitbucketPullRequest?> _pullRequestCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<BitbucketTag>> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<BitbucketTag>> _repositoryTagCache = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class VersionNameComparer : IComparer<string>
{
    public static VersionNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(x))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(y))
        {
            return -1;
        }

        var xNumbers = ExtractNumbers(x);
        var yNumbers = ExtractNumbers(y);
        var max = Math.Max(xNumbers.Count, yNumbers.Count);

        for (var index = 0; index < max; index++)
        {
            var left = index < xNumbers.Count ? xNumbers[index] : 0;
            var right = index < yNumbers.Count ? yNumbers[index] : 0;
            var compare = right.CompareTo(left);
            if (compare != 0)
            {
                return compare;
            }
        }

        return string.Compare(y, x, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<int> ExtractNumbers(string value)
    {
        var numbers = new List<int>();
        var current = 0;
        var inNumber = false;

        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                current = (current * 10) + (ch - '0');
                inNumber = true;
                continue;
            }

            if (inNumber)
            {
                numbers.Add(current);
                current = 0;
                inNumber = false;
            }
        }

        if (inNumber)
        {
            numbers.Add(current);
        }

        return numbers;
    }
}
