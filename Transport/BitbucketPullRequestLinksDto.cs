using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents the links section of a Bitbucket pull request DTO.
/// </summary>
internal sealed class BitbucketPullRequestLinksDto
{
    /// <summary>
    /// Gets or sets the HTML link for the pull request.
    /// </summary>
    [JsonPropertyName("html")]
    public BitbucketHrefDto? Html { get; set; }
}
