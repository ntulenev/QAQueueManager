using System.Net;

using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.API;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.API;

public sealed class JiraSearchExecutorTests
{
    [Fact(DisplayName = "SearchIssuesAsync requests paged search-jql results and collects all pages")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncRequestsCursorPagedResultsAndCollectsAllPages()
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

            if (requestUri.Contains("nextPageToken=page-2", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
                {
                    Issues = [issuePage2],
                    IsLast = true
                }));
            }

            requestUri.Should().Contain("search/jql");
            requestUri.Should().Contain("fields=summary%2Cstatus%2Ccustomfield_dev%2Ccustomfield_team");
            requestUri.Should().Contain("maxResults=25");
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
        var executor = new JiraSearchExecutor(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var issues = await executor.SearchIssuesAsync(
            "project = QA",
            ["summary", "status", "customfield_dev", "customfield_team"],
            25,
            cts.Token);

        // Assert
        issues.Select(static issue => issue.Key).Should().ContainInOrder("QA-101", "QA-102");
        handler.SendCalls.Should().Be(2);
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
        var executor = new JiraSearchExecutor(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var issues = await executor.SearchIssuesAsync(
            "project = QA",
            ["summary", "status", "customfield_dev"],
            50,
            cts.Token);

        // Assert
        issues.Should().ContainSingle().Which.Key.Should().Be("QA-101");
        handler.RequestUris.Should().Contain(uri => uri!.ToString().Contains("search/jql", StringComparison.Ordinal));
        handler.RequestUris.Should().Contain(uri => uri!.ToString().Contains("rest/api/3/search?", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "SearchIssuesAsync de-duplicates requested field names and clamps page size")]
    [Trait("Category", "Unit")]
    public async Task SearchIssuesAsyncDeduplicatesFieldsAndClampsPageSize()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var requestUri = request.RequestUri!.ToString();
            requestUri.Should().Contain("fields=summary%2Ccustomfield_dev%2Ccustomfield_team");
            requestUri.Should().Contain("maxResults=100");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraSearchResponse
            {
                Issues = [],
                IsLast = true
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var executor = new JiraSearchExecutor(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var issues = await executor.SearchIssuesAsync(
            "project = QA",
            ["summary", "customfield_dev", "customfield_dev", "customfield_team"],
            500,
            cts.Token);

        // Assert
        issues.Should().BeEmpty();
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
        var executor = new JiraSearchExecutor(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var issues = await executor.SearchIssuesAsync(
            "project = QA",
            ["summary", "customfield_dev"],
            25,
            cts.Token);

        // Assert
        issues.Should().ContainSingle().Which.Key.Should().Be("QA-201");
        handler.SendCalls.Should().Be(3);
    }
}
