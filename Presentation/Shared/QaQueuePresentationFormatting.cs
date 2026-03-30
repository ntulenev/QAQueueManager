using System.Globalization;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Shared formatting helpers for presentation-layer views and documents.
/// </summary>
internal static class QaQueuePresentationFormatting
{
    /// <summary>
    /// Formats the report generation timestamp for header display.
    /// </summary>
    /// <param name="value">The timestamp to format.</param>
    /// <returns>The formatted timestamp string.</returns>
    public static string FormatReportTimestamp(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats an issue timestamp for row display.
    /// </summary>
    /// <param name="value">The optional timestamp to format.</param>
    /// <returns>The formatted timestamp string or <c>-</c> when absent.</returns>
    public static string FormatIssueTimestamp(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";

    /// <summary>
    /// Formats Jira pull request links for presentation.
    /// </summary>
    /// <param name="pullRequests">The pull requests to format.</param>
    /// <returns>The formatted pull request summary.</returns>
    public static string FormatPullRequests(IReadOnlyList<JiraPullRequestLink> pullRequests) =>
        pullRequests.Count == 0
            ? "-"
            : string.Join(
                ", ",
                pullRequests.Select(static pr => $"#{pr.Id}:{pr.Status.Value}->{pr.DestinationBranch.Value}"));

    /// <summary>
    /// Formats merged pull requests for presentation.
    /// </summary>
    /// <param name="pullRequests">The merged pull requests to format.</param>
    /// <returns>The formatted pull request summary.</returns>
    public static string FormatMergedPullRequests(IReadOnlyList<QaMergedPullRequest> pullRequests) =>
        pullRequests.Count == 0
            ? "-"
            : string.Join(", ", pullRequests.Select(static pr => $"#{pr.PullRequestId}"));

    /// <summary>
    /// Formats branch names for presentation.
    /// </summary>
    /// <param name="branchNames">The branch names to format.</param>
    /// <returns>The formatted branch summary.</returns>
    public static string FormatBranchNames(IEnumerable<BranchName> branchNames)
    {
        var values = branchNames
            .Select(static branch => branch.Value)
            .Where(static branch => !string.IsNullOrWhiteSpace(branch))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count == 0 ? "-" : string.Join(", ", values);
    }

    /// <summary>
    /// Formats the duplicate-issue alert text.
    /// </summary>
    /// <param name="hasDuplicateIssue">Whether a duplicate issue alert should be shown.</param>
    /// <returns>The formatted alert text.</returns>
    public static string FormatAlertText(bool hasDuplicateIssue) =>
        hasDuplicateIssue ? MULTI_ENTRY_ALERT_TEXT : "-";

    /// <summary>
    /// Builds the Jira browse URL for an issue.
    /// </summary>
    /// <param name="jiraBrowseBaseUrl">The Jira browse base URL.</param>
    /// <param name="issueKey">The issue key.</param>
    /// <returns>The absolute Jira browse URL.</returns>
    public static string BuildIssueUrl(Uri jiraBrowseBaseUrl, JiraIssueKey issueKey) =>
        new Uri(jiraBrowseBaseUrl, Uri.EscapeDataString(issueKey.Value)).ToString();

    /// <summary>
    /// Formats a duration for telemetry display.
    /// </summary>
    /// <param name="value">The duration to format.</param>
    /// <returns>The formatted duration string.</returns>
    public static string FormatDuration(TimeSpan value)
    {
        if (value.TotalMinutes >= 1)
        {
            var wholeMinutes = (int)value.TotalMinutes;
            var seconds = value - TimeSpan.FromMinutes(wholeMinutes);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}m {1:0.000}s",
                wholeMinutes,
                seconds.TotalSeconds);
        }

        if (value.TotalSeconds >= 1)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.000}s", value.TotalSeconds);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0}ms", value.TotalMilliseconds);
    }

    /// <summary>
    /// Formats a byte count for telemetry display.
    /// </summary>
    /// <param name="value">The byte count to format.</param>
    /// <returns>The formatted size string.</returns>
    public static string FormatBytes(long value)
    {
        const double kiloByte = 1024d;
        const double megaByte = kiloByte * 1024d;

        if (value >= megaByte)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} MB", value / megaByte);
        }

        if (value >= kiloByte)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} KB", value / kiloByte);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} B", value);
    }

    private const string MULTI_ENTRY_ALERT_TEXT = "MULTI-ENTRY";
}
