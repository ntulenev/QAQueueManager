using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Stores intermediate merged pull request data while building repository sections.
/// </summary>
/// <param name="PullRequest">The merged pull request.</param>
/// <param name="Version">The resolved artifact version.</param>
internal sealed record MergedIssueData(
    BitbucketPullRequest PullRequest,
    string Version);
