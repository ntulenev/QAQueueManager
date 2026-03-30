using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using QAQueueManager.API;
using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.API;

public sealed class JiraIssueSearchClientTests
{
    [Fact(DisplayName = "SearchIssuesAsync resolves fields, executes search, and maps Jira issues")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncResolvesFieldsExecutesSearchAndMapsIssues()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issueDtos = new List<JiraIssueResponse>
        {
            new() { Id = "101", Key = "QA-101", Fields = new JiraIssueFieldsResponse() },
            new() { Id = "102", Key = "QA-102", Fields = new JiraIssueFieldsResponse() }
        };
        var expectedIssues = new List<QaIssue>
        {
            TestData.CreateIssue(101, "QA-101"),
            TestData.CreateIssue(102, "QA-102")
        };

        var fieldResolver = new Mock<IJiraFieldResolver>(MockBehavior.Strict);
        fieldResolver.Setup(r => r.ResolveRequiredFieldAsync("Development", cts.Token))
            .ReturnsAsync("customfield_dev");
        fieldResolver.Setup(r => r.ResolveOptionalFieldsAsync("Team, Squad", cts.Token))
            .ReturnsAsync(ResolvedTeamFields);

        var searchExecutor = new Mock<IJiraSearchExecutor>(MockBehavior.Strict);
        searchExecutor.Setup(e => e.SearchIssuesAsync(
                "project = QA",
                It.Is<IReadOnlyList<string>>(fields => fields.SequenceEqual(RequestedFields)),
                25,
                cts.Token))
            .ReturnsAsync(issueDtos);

        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 2 && issues[0].Key == "QA-101" && issues[1].Key == "QA-102"),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.SequenceEqual(ResolvedTeamFields))))
            .Returns(expectedIssues);

        var client = new JiraIssueSearchClient(
            fieldResolver.Object,
            searchExecutor.Object,
            mapper.Object,
            Options.Create(new JiraOptions
            {
                Jql = "project = QA",
                DevelopmentField = "Development",
                TeamField = "Team, Squad",
                MaxResultsPerPage = 25
            }));

        // Act
        var issues = await client.SearchIssuesAsync(cts.Token);

        // Assert
        issues.Should().Equal(expectedIssues);
    }

    [Fact(DisplayName = "SearchIssuesAsync preserves duplicate configured fields for executor de-duplication")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncPreservesConfiguredFieldOrderBeforeExecutorDeduplication()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var fieldResolver = new Mock<IJiraFieldResolver>(MockBehavior.Strict);
        fieldResolver.Setup(r => r.ResolveRequiredFieldAsync("customfield_dev", cts.Token))
            .ReturnsAsync("customfield_dev");
        fieldResolver.Setup(r => r.ResolveOptionalFieldsAsync("customfield_dev; Team", cts.Token))
            .ReturnsAsync(DuplicateResolvedTeamFields);

        var searchExecutor = new Mock<IJiraSearchExecutor>(MockBehavior.Strict);
        searchExecutor.Setup(e => e.SearchIssuesAsync(
                "project = QA",
                It.Is<IReadOnlyList<string>>(fields => fields.SequenceEqual(DuplicateRequestedFields)),
                200,
                cts.Token))
            .ReturnsAsync(EmptyIssueDtos);

        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 0),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.SequenceEqual(DuplicateResolvedTeamFields))))
            .Returns(EmptyIssues);

        var client = new JiraIssueSearchClient(
            fieldResolver.Object,
            searchExecutor.Object,
            mapper.Object,
            Options.Create(new JiraOptions
            {
                Jql = "project = QA",
                DevelopmentField = "customfield_dev",
                TeamField = "customfield_dev; Team",
                MaxResultsPerPage = 200
            }));

        // Act
        var issues = await client.SearchIssuesAsync(cts.Token);

        // Assert
        issues.Should().BeEmpty();
    }

    private static readonly IReadOnlyList<string> RequestedFields =
    [
        "summary",
        "status",
        "assignee",
        "updated",
        "customfield_dev",
        "customfield_team",
        "customfield_squad"
    ];

    private static readonly IReadOnlyList<string> ResolvedTeamFields =
    [
        "customfield_team",
        "customfield_squad"
    ];

    private static readonly IReadOnlyList<string> DuplicateRequestedFields =
    [
        "summary",
        "status",
        "assignee",
        "updated",
        "customfield_dev",
        "customfield_dev",
        "customfield_team"
    ];

    private static readonly IReadOnlyList<string> DuplicateResolvedTeamFields =
    [
        "customfield_dev",
        "customfield_team"
    ];

    private static readonly List<QaIssue> EmptyIssues = [];
    private static readonly JiraIssueResponse[] EmptyIssueDtos = [];
}
