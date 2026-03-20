using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Provides access to Bitbucket pull request and tag data required for the QA report.
/// </summary>
internal interface IBitbucketClient
{
    /// <summary>
    /// Loads a Bitbucket pull request by repository slug and pull request id.
    /// </summary>
    /// <param name="repositorySlug">The repository slug in Bitbucket.</param>
    /// <param name="pullRequestId">The pull request identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mapped pull request, or <see langword="null"/> when it cannot be loaded.</returns>
    Task<BitbucketPullRequest?> GetPullRequestAsync(
        RepositorySlug repositorySlug,
        PullRequestId pullRequestId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads repository tags that point to the specified commit hash.
    /// </summary>
    /// <param name="repositorySlug">The repository slug in Bitbucket.</param>
    /// <param name="commitHash">The commit hash to match against tag targets.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A list of matching tags ordered by version semantics.</returns>
    Task<IReadOnlyList<BitbucketTag>> GetTagsByCommitHashAsync(
        RepositorySlug repositorySlug,
        CommitHash commitHash,
        CancellationToken cancellationToken);
}
