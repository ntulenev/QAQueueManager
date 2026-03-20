using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

internal sealed class BitbucketPullRequestResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("updated_on")]
    public DateTimeOffset? UpdatedOn { get; set; }

    [JsonPropertyName("merge_commit")]
    public BitbucketCommitRefDto? MergeCommit { get; set; }

    [JsonPropertyName("destination")]
    public BitbucketPullRequestSideDto? Destination { get; set; }

    [JsonPropertyName("source")]
    public BitbucketPullRequestSideDto? Source { get; set; }

    [JsonPropertyName("links")]
    public BitbucketPullRequestLinksDto? Links { get; set; }
}

internal sealed class BitbucketPullRequestSideDto
{
    [JsonPropertyName("branch")]
    public BitbucketBranchDto? Branch { get; set; }

    [JsonPropertyName("repository")]
    public BitbucketRepositoryDto? Repository { get; set; }
}

internal sealed class BitbucketBranchDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class BitbucketRepositoryDto
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class BitbucketCommitRefDto
{
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}

internal sealed class BitbucketPullRequestLinksDto
{
    [JsonPropertyName("html")]
    public BitbucketHrefDto? Html { get; set; }
}

internal sealed class BitbucketHrefDto
{
    [JsonPropertyName("href")]
    public string? Href { get; set; }
}

internal sealed class BitbucketTagPageResponse
{
    [JsonPropertyName("values")]
    public List<BitbucketTagDto> Values { get; set; } = [];

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

internal sealed class BitbucketTagDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }

    [JsonPropertyName("target")]
    public BitbucketTagTargetDto? Target { get; set; }
}

internal sealed class BitbucketTagTargetDto
{
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
}
