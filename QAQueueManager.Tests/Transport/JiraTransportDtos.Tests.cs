using System.Text.Json;

using FluentAssertions;

using QAQueueManager.Transport;

namespace QAQueueManager.Tests.Transport;

public sealed class JiraTransportDtosTests
{
    [Fact(DisplayName = "Jira transport DTOs expose assigned properties")]
    [Trait("Category", "Unit")]
    public void JiraTransportDtosExposeAssignedProperties()
    {
        // Arrange
        using var summaryDocument = JsonDocument.Parse("\"Investigate flaky build\"");
        var issueFields = new JiraIssueFieldsResponse
        {
            Values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["summary"] = summaryDocument.RootElement.Clone()
            }
        };
        var searchResponse = new JiraSearchResponse
        {
            Issues =
            [
                new JiraIssueResponse
                {
                    Id = "101",
                    Key = "QA-101",
                    Fields = issueFields
                }
            ],
            NextPageToken = "page-2",
            IsLast = false,
            Total = 5
        };
        var developmentResponse = new JiraDevelopmentDetailsResponse
        {
            Detail =
            [
                new JiraDevelopmentDetailDto
                {
                    Branches =
                    [
                        new JiraBranchDto
                        {
                            Name = "feature/qa-101",
                            Repository = new JiraRepositoryDto { Name = "workspace/repo-a", Url = "https://bitbucket.example.test/workspace/repo-a" }
                        }
                    ],
                    PullRequests =
                    [
                        new JiraPullRequestDto
                        {
                            Id = "42",
                            Name = "PR 42",
                            Status = "MERGED",
                            Url = "https://bitbucket.example.test/workspace/repo-a/pull-requests/42",
                            RepositoryName = "workspace/repo-a",
                            RepositoryUrl = "https://bitbucket.example.test/workspace/repo-a",
                            Source = new JiraPullRequestBranchDto { Branch = "feature/qa-101" },
                            Destination = new JiraPullRequestBranchDto { Branch = "main" },
                            LastUpdate = "2026-03-20T08:00:00+00:00"
                        }
                    ]
                }
            ]
        };
        var fieldDefinition = new JiraFieldDefinitionResponse
        {
            Id = "customfield_100",
            Key = "customfield_100",
            Name = "Team",
            ClauseNames = ["cf[100]", "Team"]
        };

        // Assert
        searchResponse.Issues.Should().ContainSingle();
        searchResponse.Issues[0].Fields.Should().Be(issueFields);
        searchResponse.NextPageToken.Should().Be("page-2");
        developmentResponse.Detail.Should().ContainSingle();
        developmentResponse.Detail[0].Branches.Should().ContainSingle();
        developmentResponse.Detail[0].PullRequests.Should().ContainSingle();
        fieldDefinition.ClauseNames.Should().Contain("Team");
    }
}
