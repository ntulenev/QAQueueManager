using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Resolves artifact versions from Bitbucket tags attached to merged pull requests.
/// </summary>
internal sealed class ArtifactVersionResolver : IArtifactVersionResolver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactVersionResolver"/> class.
    /// </summary>
    /// <param name="bitbucketClient">The Bitbucket client.</param>
    public ArtifactVersionResolver(IBitbucketClient bitbucketClient)
    {
        ArgumentNullException.ThrowIfNull(bitbucketClient);

        _bitbucketClient = bitbucketClient;
    }

    /// <inheritdoc />
    public async Task<ArtifactVersion> ResolveAsync(
        BitbucketPullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        if (pullRequest.MergeCommitHash is null)
        {
            return ArtifactVersion.NotFound;
        }

        var tags = await _bitbucketClient
            .GetTagsByCommitHashAsync(pullRequest.RepositorySlug, pullRequest.MergeCommitHash.Value, cancellationToken)
            .ConfigureAwait(false);

        return tags.Count == 0 ? ArtifactVersion.NotFound : tags[0].Name;
    }

    private readonly IBitbucketClient _bitbucketClient;
}
