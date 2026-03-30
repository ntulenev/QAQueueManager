using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Resolves the artifact version associated with a merged pull request.
/// </summary>
internal interface IArtifactVersionResolver
{
    /// <summary>
    /// Resolves the artifact version for the supplied pull request.
    /// </summary>
    /// <param name="pullRequest">The merged Bitbucket pull request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The resolved artifact version.</returns>
    Task<ArtifactVersion> ResolveAsync(BitbucketPullRequest pullRequest, CancellationToken cancellationToken);
}
