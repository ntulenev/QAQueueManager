namespace QAQueueManager.Logic;

/// <summary>
/// Best-effort counts extracted from the Jira Development field summary.
/// </summary>
internal readonly record struct JiraDevelopmentSummarySnapshot(
    int? PullRequestCount,
    int? BranchCount)
{
    /// <summary>
    /// Gets a value indicating whether the summary confidently reports no linked pull requests or branches.
    /// </summary>
    public bool HasKnownNoDevelopment => PullRequestCount == 0 && BranchCount == 0;

    /// <summary>
    /// Gets a value indicating whether the summary confidently reports no linked pull requests.
    /// </summary>
    public bool HasNoPullRequests => PullRequestCount == 0;

    /// <summary>
    /// Gets a value indicating whether the summary confidently reports no linked branches.
    /// </summary>
    public bool HasNoBranches => BranchCount == 0;
}
