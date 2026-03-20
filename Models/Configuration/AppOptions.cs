using System.ComponentModel.DataAnnotations;

namespace QAQueueManager.Models.Configuration;

internal sealed class JiraOptions
{
    [Required]
    public Uri BaseUrl { get; init; } = null!;

    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string ApiToken { get; init; } = string.Empty;

    [Required]
    public string Jql { get; init; } = string.Empty;

    [Required]
    public string DevelopmentField { get; init; } = "Development";

    public string TeamField { get; init; } = string.Empty;

    [Range(1, 100)]
    public int MaxResultsPerPage { get; init; } = 100;

    [Range(0, 10)]
    public int RetryCount { get; init; } = 3;

    [Required]
    public string BitbucketApplicationType { get; init; } = "bitbucket";

    [Required]
    public string PullRequestDataType { get; init; } = "pullrequest";

    [Required]
    public string BranchDataType { get; init; } = "branch";
}

internal sealed class BitbucketOptions
{
    [Required]
    public Uri BaseUrl { get; init; } = null!;

    [Required]
    public string Workspace { get; init; } = string.Empty;

    [Required]
    public string AuthEmail { get; init; } = string.Empty;

    [Required]
    public string AuthApiToken { get; init; } = string.Empty;

    [Range(0, 10)]
    public int RetryCount { get; init; } = 3;
}

internal sealed class ReportOptions
{
    [Required]
    public string Title { get; init; } = "QA Queue By Repository";

    [Required]
    public string TargetBranch { get; init; } = string.Empty;

    [Required]
    public string PdfOutputPath { get; init; } = "qa-queue-report.pdf";

    [Required]
    public string ExcelOutputPath { get; init; } = "qa-queue-report.xlsx";

    [Range(1, 32)]
    public int MaxParallelism { get; init; } = 4;

    public bool HideNoCodeIssues { get; init; }

    public bool OpenAfterGeneration { get; init; }
}
