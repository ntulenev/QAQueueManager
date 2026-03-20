using System.ComponentModel.DataAnnotations;

namespace QAQueueManager.Models.Configuration;

/// <summary>
/// Represents Bitbucket connection settings.
/// </summary>
internal sealed class BitbucketOptions
{
    /// <summary>
    /// Gets the Bitbucket base URL.
    /// </summary>
    [Required]
    public Uri BaseUrl { get; init; } = null!;

    /// <summary>
    /// Gets the Bitbucket workspace name.
    /// </summary>
    [Required]
    public string Workspace { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Bitbucket account email used for authentication.
    /// </summary>
    [Required]
    public string AuthEmail { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Bitbucket API token or app password.
    /// </summary>
    [Required]
    public string AuthApiToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of retries for Bitbucket HTTP calls.
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; init; } = 3;
}
