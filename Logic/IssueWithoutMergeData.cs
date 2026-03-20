using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Stores intermediate no-merge data while building repository sections.
/// </summary>
/// <param name="PullRequests">The related pull requests.</param>
/// <param name="BranchNames">The related branch names.</param>
internal sealed record IssueWithoutMergeData(
    IReadOnlyList<JiraPullRequestLink> PullRequests,
    IReadOnlyList<string> BranchNames);
