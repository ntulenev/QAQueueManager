namespace QAQueueManager.Models.Domain;

/// <summary>
/// Stores intermediate merged pull request data while building repository sections.
/// </summary>
/// <param name="PullRequest">The merged pull request.</param>
/// <param name="Version">The resolved artifact version.</param>
internal sealed record MergedIssueData(
    BitbucketPullRequest PullRequest,
    ArtifactVersion Version)
{
    /// <summary>
    /// Creates an intermediate merged payload.
    /// </summary>
    /// <param name="pullRequest">The merged pull request.</param>
    /// <param name="version">The resolved artifact version.</param>
    /// <returns>The merged payload.</returns>
    public static MergedIssueData Create(BitbucketPullRequest pullRequest, ArtifactVersion version)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);

        return new MergedIssueData(pullRequest, version);
    }
}
