using System.Net;

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
    [Fact(DisplayName = "SearchIssuesAsync resolves fields, requests paged search results, and maps all pages")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncResolvesFieldsRequestsPagedSearchResultsAndMapsAllPages()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issuePage1 = new JiraIssueResponse { Id = "101", Key = "QA-101", Fields = new JiraIssueFieldsResponse() };
        var issuePage2 = new JiraIssueResponse { Id = "102", Key = "QA-102", Fields = new JiraIssueFieldsResponse() };
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            var requestUri = request.RequestUri!.ToString();

            if (requestUri.Contains("rest/api/3/field", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new[]
                {
                    new JiraFieldDefinitionResponse
                    {
                        Id = "customfield_dev",
                        Key = "customfield_dev",
                        Name = "Development"
                    },
                    new JiraFieldDefinitionResponse
                    {
                        Id = "customfield_team",
                        Key = "customfield_team",
                        Name = "Team"
                    }
                }));
            }

            if (requestUri.Contains("nextPageToken=page-2", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
                {
                    Issues = [issuePage2],
                    IsLast = true
                }));
            }

            requestUri.Should().Contain("search/jql");
            requestUri.Should().Contain("fields=summary%2Cstatus%2Cupdated%2Ccustomfield_dev%2Ccustomfield_team");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
            {
                Issues = [issuePage1],
                IsLast = false,
                NextPageToken = "page-2"
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));

        var mapperCalls = 0;
        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.SimplifyAlias("Development"))
            .Callback(() => mapperCalls++)
            .Returns("Development");
        mapper.Setup(m => m.SimplifyAlias("Team"))
            .Callback(() => mapperCalls++)
            .Returns("Team");
        mapper.Setup(m => m.BuildFieldLookup(It.Is<IEnumerable<JiraFieldDefinitionResponse>>(fields =>
                fields.Count() == 2
                && fields.Any(static field => field.Key == "customfield_dev")
                && fields.Any(static field => field.Key == "customfield_team"))))
            .Callback(() => mapperCalls++)
            .Returns(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Development"] = ["customfield_dev"],
                ["Team"] = ["customfield_team"]
            });
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 1 && issues[0].Id == "101" && issues[0].Key == "QA-101"),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.Count == 1 && fields[0] == "customfield_team")))
            .Callback(() => mapperCalls++)
            .Returns([TestData.CreateIssue(101, "QA-101")]);
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 1 && issues[0].Id == "102" && issues[0].Key == "QA-102"),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.Count == 1 && fields[0] == "customfield_team")))
            .Callback(() => mapperCalls++)
            .Returns([TestData.CreateIssue(102, "QA-102")]);

        var client = new JiraIssueSearchClient(transport, mapper.Object, Options.Create(new JiraOptions
        {
            Jql = "project = QA",
            DevelopmentField = "Development",
            TeamField = "Team",
            MaxResultsPerPage = 25,
            RetryCount = 0
        }));

        // Act
        var issues = await client.SearchIssuesAsync(cts.Token);

        // Assert
        issues.Select(static issue => issue.Key.Value).Should().ContainInOrder("QA-101", "QA-102");
        handler.SendCalls.Should().Be(3);
        mapperCalls.Should().Be(5);
    }

    [Fact(DisplayName = "SearchIssuesAsync falls back to legacy Jira search when search-jql endpoint is missing")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncWhenSearchJqlIsMissingFallsBackToLegacySearch()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = new JiraIssueResponse { Id = "101", Key = "QA-101", Fields = new JiraIssueFieldsResponse() };
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var requestUri = request.RequestUri!.ToString();
            if (requestUri.Contains("rest/api/3/field", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new[]
                {
                    new JiraFieldDefinitionResponse
                    {
                        Id = "customfield_dev",
                        Key = "customfield_dev",
                        Name = "Development"
                    }
                }));
            }

            if (requestUri.Contains("search/jql", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{}""", HttpStatusCode.NotFound));
            }

            requestUri.Should().Contain("rest/api/3/search?");
            requestUri.Should().Contain("startAt=0");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
            {
                Issues = [issue],
                Total = 1
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));
        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.SimplifyAlias("Development"))
            .Callback(() => { })
            .Returns("Development");
        mapper.Setup(m => m.BuildFieldLookup(It.Is<IEnumerable<JiraFieldDefinitionResponse>>(fields =>
                fields.Single().Key == "customfield_dev")))
            .Callback(() => { })
            .Returns(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Development"] = ["customfield_dev"]
            });
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 1 && issues[0].Id == "101" && issues[0].Key == "QA-101"),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.Count == 0)))
            .Callback(() => { })
            .Returns([TestData.CreateIssue(101, "QA-101")]);

        var client = new JiraIssueSearchClient(transport, mapper.Object, Options.Create(new JiraOptions
        {
            Jql = "project = QA",
            DevelopmentField = "Development",
            MaxResultsPerPage = 50,
            RetryCount = 0
        }));

        // Act
        var issues = await client.SearchIssuesAsync(cts.Token);

        // Assert
        issues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-101"));
        handler.RequestUris.Should().Contain(uri => uri!.ToString().Contains("search/jql", StringComparison.Ordinal));
        handler.RequestUris.Should().Contain(uri => uri!.ToString().Contains("rest/api/3/search?", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "SearchIssuesAsync reuses cached direct custom fields and de-duplicates requested field names")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncWithDirectCustomFieldsUsesCachedFieldResolution()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var issue = new JiraIssueResponse { Id = "103", Key = "QA-103", Fields = new JiraIssueFieldsResponse() };
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var requestUri = request.RequestUri!.ToString();
            requestUri.Should().Contain("search/jql");
            requestUri.Should().Contain("fields=summary%2Cstatus%2Cupdated%2Ccustomfield_dev");
            requestUri.Should().NotContain("rest/api/3/field");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
            {
                Issues = [issue],
                IsLast = true
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));
        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.SimplifyAlias("customfield_dev")).Returns("customfield_dev");
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 1 && issues[0].Id == "103"),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.Count == 1 && fields[0] == "customfield_dev")))
            .Returns([TestData.CreateIssue(103, "QA-103")]);

        var client = new JiraIssueSearchClient(transport, mapper.Object, Options.Create(new JiraOptions
        {
            Jql = "project = QA",
            DevelopmentField = "customfield_dev",
            TeamField = "customfield_dev; customfield_dev",
            MaxResultsPerPage = 25,
            RetryCount = 0
        }));

        // Act
        var issues = await client.SearchIssuesAsync(cts.Token);

        // Assert
        issues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-103"));
        handler.SendCalls.Should().Be(1);
    }

    [Fact(DisplayName = "SearchIssuesAsync throws when a configured Jira field alias cannot be resolved")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncWhenConfiguredFieldCannotBeResolvedThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            request.RequestUri!.ToString().Should().Contain("rest/api/3/field");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new[]
            {
                new JiraFieldDefinitionResponse
                {
                    Id = "customfield_other",
                    Key = "customfield_other",
                    Name = "Other"
                }
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));
        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.SimplifyAlias("Development")).Returns("development");
        mapper.Setup(m => m.BuildFieldLookup(It.IsAny<IEnumerable<JiraFieldDefinitionResponse>>()))
            .Returns(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));

        var client = new JiraIssueSearchClient(transport, mapper.Object, Options.Create(new JiraOptions
        {
            Jql = "project = QA",
            DevelopmentField = "Development",
            MaxResultsPerPage = 25,
            RetryCount = 0
        }));

        // Act
        var act = async () => await client.SearchIssuesAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development*");
    }

    [Fact(DisplayName = "SearchIssuesAsync legacy fallback continues until Jira returns an empty page")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncLegacyFallbackContinuesUntilEmptyPage()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var firstIssue = new JiraIssueResponse { Id = "201", Key = "QA-201", Fields = new JiraIssueFieldsResponse() };
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var requestUri = request.RequestUri!.ToString();
            if (requestUri.Contains("search/jql", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{}""", HttpStatusCode.NotFound));
            }

            if (requestUri.Contains("startAt=0", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
                {
                    Issues = [firstIssue],
                    Total = 2
                }));
            }

            requestUri.Should().Contain("startAt=1");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
            {
                Issues = []
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));
        var mapper = new Mock<IJiraIssueSearchMapper>(MockBehavior.Strict);
        mapper.Setup(m => m.SimplifyAlias("customfield_dev")).Returns("customfield_dev");
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 1 && issues[0].Id == "201"),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.Count == 0)))
            .Returns([TestData.CreateIssue(201, "QA-201")]);
        mapper.Setup(m => m.MapIssues(
                It.Is<List<JiraIssueResponse>>(issues => issues.Count == 0),
                "customfield_dev",
                It.Is<IReadOnlyList<string>>(fields => fields.Count == 0)))
            .Returns([]);

        var client = new JiraIssueSearchClient(transport, mapper.Object, Options.Create(new JiraOptions
        {
            Jql = "project = QA",
            DevelopmentField = "customfield_dev",
            MaxResultsPerPage = 25,
            RetryCount = 0
        }));

        // Act
        var issues = await client.SearchIssuesAsync(cts.Token);

        // Assert
        issues.Should().ContainSingle().Which.Key.Should().Be(new JiraIssueKey("QA-201"));
        handler.SendCalls.Should().Be(3);
    }
}
