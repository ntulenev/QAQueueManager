using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents one side of a Bitbucket pull request.
/// </summary>
internal sealed class BitbucketPullRequestSideDto
{
    /// <summary>
    /// Gets or sets the branch details.
    /// </summary>
    [JsonPropertyName("branch")]
    public BitbucketBranchDto? Branch { get; set; }

    /// <summary>
    /// Gets or sets the repository details.
    /// </summary>
    [JsonPropertyName("repository")]
    public BitbucketRepositoryDto? Repository { get; set; }
}
