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

        var lazyTask = _pullRequestInFlight.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<BitbucketPullRequest?>>(
                () => LoadPullRequestCoreAsync(cacheKey, repositorySlug, pullRequestId, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyTask.Value.ConfigureAwait(false);
        }
        finally
        {
            _ = _pullRequestInFlight.TryRemove(
                new KeyValuePair<string, Lazy<Task<BitbucketPullRequest?>>>(cacheKey, lazyTask));
        }
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
        if (_tagLookupFailureCache.ContainsKey(repositorySlug))
        {
            return [];
        }

        var cacheKey = $"{repositorySlug}@{commitHash}";
        if (_tagCache.TryGetValue(cacheKey, out var cachedTags))
        {
            return cachedTags;
        }

        var lazyTask = _tagInFlight.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<IReadOnlyList<BitbucketTag>>>(
                () => LoadTagsByCommitHashCoreAsync(cacheKey, repositorySlug, commitHash, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyTask.Value.ConfigureAwait(false);
        }
        finally
        {
            _ = _tagInFlight.TryRemove(
                new KeyValuePair<string, Lazy<Task<IReadOnlyList<BitbucketTag>>>>(cacheKey, lazyTask));
        }
    }

    private async Task<BitbucketPullRequest?> LoadPullRequestCoreAsync(
        string cacheKey,
        RepositorySlug repositorySlug,
        PullRequestId pullRequestId,
        CancellationToken cancellationToken)
    {
        var url = new Uri(
            $"repositories/{_options.Workspace}/{Uri.EscapeDataString(repositorySlug.Value)}/pullrequests/{pullRequestId.Value}" +
            $"?fields={Uri.EscapeDataString("id,state,updated_on,merge_commit.hash,source.branch.name,destination.branch.name,destination.repository.full_name,destination.repository.name,links.html.href")}",
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
        var htmlUrl = CreateUriOrNull(response.Links?.Html?.Href);
        var mergeCommitHash = CommitHash.TryCreate(response.MergeCommit?.Hash, out var parsedMergeCommitHash)
            ? (CommitHash?)parsedMergeCommitHash
            : null;

        var mapped = new BitbucketPullRequest(
            new PullRequestId(response.Id),
            string.IsNullOrWhiteSpace(response.State) ? PullRequestState.Unknown : new PullRequestState(response.State),
            new RepositoryFullName(string.IsNullOrWhiteSpace(repositoryFullName)
                ? $"{_options.Workspace}/{repositorySlug.Value}"
                : repositoryFullName),
            new RepositoryDisplayName(string.IsNullOrWhiteSpace(repositoryDisplayName) ? repositorySlug.Value : repositoryDisplayName),
            repositorySlug,
            string.IsNullOrWhiteSpace(response.Source?.Branch?.Name) ? BranchName.Unknown : new BranchName(response.Source.Branch.Name),
            string.IsNullOrWhiteSpace(response.Destination?.Branch?.Name) ? BranchName.Unknown : new BranchName(response.Destination.Branch.Name),
            htmlUrl,
            mergeCommitHash,
            response.UpdatedOn);

        _pullRequestCache[cacheKey] = mapped;
        return mapped;
    }

    private async Task<IReadOnlyList<BitbucketTag>> LoadTagsByCommitHashCoreAsync(
        string cacheKey,
        RepositorySlug repositorySlug,
        CommitHash commitHash,
        CancellationToken cancellationToken)
    {
        var tags = new List<BitbucketTag>();
        var next = BuildTagLookupUri(repositorySlug, commitHash);

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
                _tagLookupFailureCache[repositorySlug] = true;
                return [];
            }

            if (response?.Values is not null)
            {
                tags.AddRange(
                    response.Values
                        .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
                        .Select(static item => new BitbucketTag(
                            new ArtifactVersion(item.Name!),
                            CommitHash.TryCreate(item.Target?.Hash, out var hash) ? hash : null,
                            item.Date)));
            }

            next = CreateNextUri(response?.Next);
        }

        IReadOnlyList<BitbucketTag> distinctTags = [.. tags
            .GroupBy(static tag => tag.Name.Value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static tag => tag.Name, VersionNameComparer.Instance)];

        _tagCache[cacheKey] = distinctTags;
        return distinctTags;
    }

    private Uri BuildTagLookupUri(RepositorySlug repositorySlug, CommitHash commitHash)
    {
        var q = $"target.hash = \"{commitHash.Value}\"";
        const string fields = "values.name,values.date,values.target.hash,next";
        var url =
            $"repositories/{_options.Workspace}/{Uri.EscapeDataString(repositorySlug.Value)}/refs/tags" +
            $"?pagelen=100&q={Uri.EscapeDataString(q)}&fields={Uri.EscapeDataString(fields)}";

        return new Uri(url, UriKind.Relative);
    }

    private static Uri? CreateNextUri(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            ? absolute
            : Uri.TryCreate(value, UriKind.Relative, out var relative)
            ? relative
            : null;
    }

    private static Uri? CreateUriOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ?
        null :
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ? uri : null;

    private readonly BitbucketTransport _transport;
    private readonly BitbucketOptions _options;
    private readonly ConcurrentDictionary<string, BitbucketPullRequest?> _pullRequestCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<BitbucketTag>> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<BitbucketPullRequest?>>> _pullRequestInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<BitbucketTag>>>> _tagInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<RepositorySlug, bool> _tagLookupFailureCache = [];
}
