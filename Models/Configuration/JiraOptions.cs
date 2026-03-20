using System.ComponentModel.DataAnnotations;

namespace QAQueueManager.Models.Configuration;

/// <summary>
/// Represents Jira connection and query settings.
/// </summary>
internal sealed class JiraOptions
{
    /// <summary>
    /// Gets the Jira base URL.
    /// </summary>
    [Required]
    public Uri BaseUrl { get; init; } = null!;

    /// <summary>
    /// Gets the Jira account email used for authentication.
    /// </summary>
    [Required]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Jira API token.
    /// </summary>
    [Required]
    public string ApiToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the JQL query used to load QA issues.
    /// </summary>
    [Required]
    public string Jql { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Jira field name or id used to resolve development links.
    /// </summary>
    [Required]
    public string DevelopmentField { get; init; } = "Development";

    /// <summary>
    /// Gets the optional Jira team field aliases used for team grouping.
    /// </summary>
    public string TeamField { get; init; } = string.Empty;

    /// <summary>
    /// Gets the maximum number of Jira issues fetched per page.
    /// </summary>
    [Range(1, 100)]
    public int MaxResultsPerPage { get; init; } = 100;

    /// <summary>
    /// Gets the number of retries for Jira HTTP calls.
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// Gets the Jira development application type used in dev-status requests.
    /// </summary>
    [Required]
    public string BitbucketApplicationType { get; init; } = "bitbucket";

    /// <summary>
    /// Gets the Jira dev-status data type used for pull requests.
    /// </summary>
    [Required]
    public string PullRequestDataType { get; init; } = "pullrequest";

    /// <summary>
    /// Gets the Jira dev-status data type used for branches.
    /// </summary>
    [Required]
    public string BranchDataType { get; init; } = "branch";
}
