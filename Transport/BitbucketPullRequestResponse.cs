using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

/// <summary>
/// Represents a Bitbucket pull request API response.
/// </summary>
internal sealed class BitbucketPullRequestResponse
{
    /// <summary>
    /// Gets or sets the pull request identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the pull request state.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    [JsonPropertyName("updated_on")]
    public DateTimeOffset? UpdatedOn { get; set; }

    /// <summary>
    /// Gets or sets the merge commit reference.
    /// </summary>
    [JsonPropertyName("merge_commit")]
    public BitbucketCommitRefDto? MergeCommit { get; set; }

    /// <summary>
    /// Gets or sets the destination branch side.
    /// </summary>
    [JsonPropertyName("destination")]
    public BitbucketPullRequestSideDto? Destination { get; set; }

    /// <summary>
    /// Gets or sets the source branch side.
    /// </summary>
    [JsonPropertyName("source")]
    public BitbucketPullRequestSideDto? Source { get; set; }

    /// <summary>
    /// Gets or sets the hyperlink collection.
    /// </summary>
    [JsonPropertyName("links")]
    public BitbucketPullRequestLinksDto? Links { get; set; }
}
