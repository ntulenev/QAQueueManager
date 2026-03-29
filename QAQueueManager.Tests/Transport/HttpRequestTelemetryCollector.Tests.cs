using FluentAssertions;

using QAQueueManager.Transport;

namespace QAQueueManager.Tests.Transport;

public sealed class HttpRequestTelemetryCollectorTests
{
    [Fact(DisplayName = "Record aggregates requests by source method and endpoint")]
    [Trait("Category", "Unit")]
    public void RecordWhenRequestsShareEndpointAggregatesIntoSingleEntry()
    {
        // Arrange
        var collector = new HttpRequestTelemetryCollector();

        // Act
        collector.Record(
            "Jira",
            "get",
            new Uri("rest/api/3/search?startAt=0", UriKind.Relative),
            TimeSpan.FromMilliseconds(150),
            responseBytes: 128,
            isRetry: false);
        collector.Record(
            "Jira",
            "GET",
            new Uri("rest/api/3/search?startAt=50", UriKind.Relative),
            TimeSpan.FromMilliseconds(50),
            responseBytes: 64,
            isRetry: true);
        collector.Record(
            "Bitbucket",
            "GET",
            new Uri("https://bitbucket.example.test/repositories/ws/repo-a?pagelen=100", UriKind.Absolute),
            TimeSpan.FromMilliseconds(25),
            responseBytes: 32,
            isRetry: false);

        var summary = collector.GetSummary();

        // Assert
        summary.RequestCount.Should().Be(3);
        summary.RetryCount.Should().Be(1);
        summary.ResponseBytes.Should().Be(224);
        summary.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(225));

        var jiraEndpoint = summary.Endpoints
            .Should().ContainSingle(endpoint =>
                endpoint.Source == "Jira"
                && endpoint.Method == "GET"
                && endpoint.Endpoint == "/rest/api/3/search")
            .Which;

        jiraEndpoint.RequestCount.Should().Be(2);
        jiraEndpoint.RetryCount.Should().Be(1);
        jiraEndpoint.ResponseBytes.Should().Be(192);
        jiraEndpoint.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(200));
        jiraEndpoint.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(150));

        var bitbucketEndpoint = summary.Endpoints
            .Should().ContainSingle(endpoint =>
                endpoint.Source == "Bitbucket"
                && endpoint.Method == "GET"
                && endpoint.Endpoint == "/repositories/ws/repo-a")
            .Which;

        bitbucketEndpoint.RequestCount.Should().Be(1);
        bitbucketEndpoint.RetryCount.Should().Be(0);
        bitbucketEndpoint.ResponseBytes.Should().Be(32);
        bitbucketEndpoint.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(25));
        bitbucketEndpoint.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(25));
    }

    [Fact(DisplayName = "Reset clears accumulated telemetry")]
    [Trait("Category", "Unit")]
    public void ResetWhenCalledClearsCollectedState()
    {
        // Arrange
        var collector = new HttpRequestTelemetryCollector();
        collector.Record(
            "Jira",
            "GET",
            new Uri("rest/api/3/field", UriKind.Relative),
            TimeSpan.FromMilliseconds(10),
            responseBytes: 16,
            isRetry: false);

        // Act
        collector.Reset();
        var summary = collector.GetSummary();

        // Assert
        summary.RequestCount.Should().Be(0);
        summary.RetryCount.Should().Be(0);
        summary.ResponseBytes.Should().Be(0);
        summary.TotalDuration.Should().Be(TimeSpan.Zero);
        summary.Endpoints.Should().BeEmpty();
    }
}
