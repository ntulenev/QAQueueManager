using System.Text.Json;

using FluentAssertions;

using QAQueueManager.API;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.Tests.API;

public sealed class JiraIssueSearchMapperTests
{
    [Fact(DisplayName = "MapIssues maps issue fields and de-duplicates teams")]
    [Trait("Category", "Unit")]
    public void MapIssuesWhenIssueContainsTeamFieldMapsTypedIssue()
    {
        // Arrange
        var mapper = CreateMapper();
        var issue = new JiraIssueResponse
        {
            Id = "101",
            Key = "QA-101",
            Fields = new JiraIssueFieldsResponse
            {
                Values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["summary"] = JsonSerializer.SerializeToElement("Investigate flaky build"),
                    ["status"] = JsonSerializer.SerializeToElement(new { name = "In Progress" }),
                    ["assignee"] = JsonSerializer.SerializeToElement(new { name = "qa-login", displayName = "Jane Doe" }),
                    ["updated"] = JsonSerializer.SerializeToElement("2026-03-20T10:15:00+00:00"),
                    ["development"] = JsonSerializer.SerializeToElement(/*lang=json,strict*/ """{"branches":1}"""),
                    ["customfield_100"] = JsonSerializer.SerializeToElement(TeamValues)
                }
            }
        };

        // Act
        var issues = mapper.MapIssues([issue], "development", TeamApiFields);

        // Assert
        issues.Should().ContainSingle();
        issues[0].Id.Should().Be(new JiraIssueId(101));
        issues[0].Key.Should().Be(new JiraIssueKey("QA-101"));
        issues[0].Status.Should().Be(new JiraIssueStatus("In Progress"));
        issues[0].Assignee.Should().Be("Jane Doe");
        issues[0].Teams.Should().ContainInOrder(new TeamName("Core"), new TeamName("Platform"));
        issues[0].UpdatedAt.Should().Be(new DateTimeOffset(2026, 3, 20, 10, 15, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "MapIssues skips invalid issues and uses fallback display values")]
    [Trait("Category", "Unit")]
    public void MapIssuesWhenIssueContainsInvalidOrComplexValuesUsesFallbacks()
    {
        // Arrange
        var mapper = CreateMapper();
        List<JiraIssueResponse> issues =
        [
            new JiraIssueResponse
            {
                Id = "invalid",
                Key = "QA-0",
                Fields = new JiraIssueFieldsResponse()
            },
            new JiraIssueResponse
            {
                Id = "102",
                Key = "QA-102",
                Fields = new JiraIssueFieldsResponse
                {
                    Values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["summary"] = JsonSerializer.SerializeToElement(42),
                        ["status"] = JsonSerializer.SerializeToElement(false),
                        ["updated"] = JsonSerializer.SerializeToElement("not-a-date"),
                        ["development"] = JsonSerializer.SerializeToElement(new { unexpected = "value" }),
                        ["customfield_100"] = JsonSerializer.SerializeToElement(new object?[]
                        {
                            new { displayName = "Core" },
                            new { value = "core" },
                            new { key = "Platform" },
                            " platform "
                        })
                    }
                }
            },
            new JiraIssueResponse
            {
                Id = "103",
                Key = " ",
                Fields = new JiraIssueFieldsResponse()
            }
        ];

        // Act
        var mapped = mapper.MapIssues(issues, "development", ["customfield_100", "customfield_101"]);

        // Assert
        mapped.Should().ContainSingle();
        mapped[0].Id.Should().Be(new JiraIssueId(102));
        mapped[0].Summary.Should().Be("42");
        mapped[0].Status.Should().Be(new JiraIssueStatus(bool.FalseString));
        mapped[0].DevelopmentSummary.Should().Be("""{"unexpected":"value"}""");
        mapped[0].Teams.Should().ContainInOrder(new TeamName("Core"), new TeamName("Platform"));
        mapped[0].UpdatedAt.Should().BeNull();
    }

    [Fact(DisplayName = "MapIssues returns default values when optional fields and teams are not configured")]
    [Trait("Category", "Unit")]
    public void MapIssuesWhenOptionalFieldsAreMissingUsesDefaults()
    {
        // Arrange
        var mapper = CreateMapper();
        List<JiraIssueResponse> issues =
        [
            new JiraIssueResponse
            {
                Id = "104",
                Key = "QA-104",
                Fields = new JiraIssueFieldsResponse()
            }
        ];

        // Act
        var mapped = mapper.MapIssues(issues, "development", []);

        // Assert
        mapped.Should().ContainSingle();
        mapped[0].Summary.Should().Be("-");
        mapped[0].Status.Should().Be(JiraIssueStatus.Unknown);
        mapped[0].Assignee.Should().Be("-");
        mapped[0].DevelopmentSummary.Should().Be("{}");
        mapped[0].Teams.Should().BeEmpty();
        mapped[0].UpdatedAt.Should().BeNull();
    }

    private static JiraIssueSearchMapper CreateMapper() => new(new JiraObjectMapper());

    private static readonly IReadOnlyList<string> TeamApiFields = ["customfield_100"];
    private static readonly string[] TeamValues = ["Core", " core ", "Platform"];
}
