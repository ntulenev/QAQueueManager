using System.Text.Json;
using System.Text.Json.Serialization;

namespace QAQueueManager.Transport;

internal sealed class JiraSearchResponse
{
    [JsonPropertyName("issues")]
    public List<JiraIssueResponse> Issues { get; set; } = [];

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }

    [JsonPropertyName("isLast")]
    public bool IsLast { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

internal sealed class JiraIssueResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("fields")]
    public JiraIssueFieldsResponse? Fields { get; set; }
}

internal sealed class JiraIssueFieldsResponse
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Values { get; set; } = [];
}

internal sealed class JiraFieldDefinitionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("clauseNames")]
    public List<string> ClauseNames { get; set; } = [];
}

internal sealed class JiraDevelopmentDetailsResponse
{
    [JsonPropertyName("detail")]
    public List<JiraDevelopmentDetailDto> Detail { get; set; } = [];
}

internal sealed class JiraDevelopmentDetailDto
{
    [JsonPropertyName("branches")]
    public List<JiraBranchDto> Branches { get; set; } = [];

    [JsonPropertyName("pullRequests")]
    public List<JiraPullRequestDto> PullRequests { get; set; } = [];
}

internal sealed class JiraBranchDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("repository")]
    public JiraRepositoryDto? Repository { get; set; }
}

internal sealed class JiraRepositoryDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class JiraPullRequestDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("repositoryName")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("source")]
    public JiraPullRequestBranchDto? Source { get; set; }

    [JsonPropertyName("destination")]
    public JiraPullRequestBranchDto? Destination { get; set; }

    [JsonPropertyName("lastUpdate")]
    public string? LastUpdate { get; set; }
}

internal sealed class JiraPullRequestBranchDto
{
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
}
