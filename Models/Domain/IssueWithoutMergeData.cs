namespace QAQueueManager.Models.Domain;

/// <summary>
/// Stores intermediate no-merge data while building repository sections.
/// </summary>
/// <param name="PullRequests">The related pull requests.</param>
/// <param name="BranchNames">The related branch names.</param>
internal sealed record IssueWithoutMergeData(
    IReadOnlyList<JiraPullRequestLink> PullRequests,
    IReadOnlyList<BranchName> BranchNames)
{
    /// <summary>
    /// Creates an intermediate no-merge payload.
    /// </summary>
    /// <param name="pullRequests">The related pull requests.</param>
    /// <param name="branchNames">The related branch names.</param>
    /// <returns>The no-merge payload.</returns>
    public static IssueWithoutMergeData Create(
        IReadOnlyList<JiraPullRequestLink> pullRequests,
        IReadOnlyList<BranchName> branchNames)
    {
        ArgumentNullException.ThrowIfNull(pullRequests);
        ArgumentNullException.ThrowIfNull(branchNames);

        return new IssueWithoutMergeData(pullRequests, branchNames);
    }
}
