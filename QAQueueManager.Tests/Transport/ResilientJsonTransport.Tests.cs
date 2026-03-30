using System.Net;

using FluentAssertions;

using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Transport;

public sealed class ResilientJsonTransportTests
{
    [Fact(DisplayName = "GetAsync deserializes successful JSON responses")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsSuccessfulDeserializesPayload()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            request.RequestUri.Should().Be(new Uri("https://transport.example.test/resource", UriKind.Absolute));
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new Dictionary<string, string> { ["value"] = "ok" }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://transport.example.test/", UriKind.Absolute)
        };
        var telemetryCollector = new HttpRequestTelemetryCollector();
        var transport = new TestResilientJsonTransport(httpClient, retryCount: 0, telemetryCollector);

        // Act
        var response = await transport.GetAsync<Dictionary<string, string>>(new Uri("resource", UriKind.Relative), cts.Token);
        var telemetry = telemetryCollector.GetSummary();

        // Assert
        response.Should().NotBeNull();
        response!["value"].Should().Be("ok");
        handler.SendCalls.Should().Be(1);
        telemetry.RequestCount.Should().Be(1);
        telemetry.RetryCount.Should().Be(0);
        telemetry.ResponseBytes.Should().BeGreaterThan(0);
        telemetry.Endpoints.Should().ContainSingle(endpoint =>
            endpoint.Source == TestResilientJsonTransport.SourceName
            && endpoint.Method == "GET"
            && endpoint.Endpoint == "/resource"
            && endpoint.RequestCount == 1);
    }

    [Fact(DisplayName = "GetAsync retries retriable responses before succeeding")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsRetriableRetriesBeforeSucceeding()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var sendCalls = 0;
        using var handler = new RecordingHttpMessageHandler((_, _) =>
        {
            sendCalls++;
            return Task.FromResult(sendCalls == 1
                ? RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{}""", HttpStatusCode.TooManyRequests)
                : RecordingHttpMessageHandler.CreateJsonResponse(new Dictionary<string, string> { ["value"] = "retried" }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://transport.example.test/", UriKind.Absolute)
        };
        var telemetryCollector = new HttpRequestTelemetryCollector();
        var transport = new TestResilientJsonTransport(httpClient, retryCount: 1, telemetryCollector);

        // Act
        var response = await transport.GetAsync<Dictionary<string, string>>(new Uri("resource", UriKind.Relative), cts.Token);
        var telemetry = telemetryCollector.GetSummary();

        // Assert
        response.Should().NotBeNull();
        response!["value"].Should().Be("retried");
        sendCalls.Should().Be(2);
        telemetry.RequestCount.Should().Be(2);
        telemetry.RetryCount.Should().Be(1);
        telemetry.Endpoints.Should().ContainSingle(endpoint =>
            endpoint.Source == TestResilientJsonTransport.SourceName
            && endpoint.Endpoint == "/resource"
            && endpoint.RequestCount == 2
            && endpoint.RetryCount == 1);
    }

    [Fact(DisplayName = "GetAsync throws for non-retriable failures")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsNonRetriableThrowsHttpRequestException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{"error":"forbidden"}""", HttpStatusCode.BadRequest)));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://transport.example.test/", UriKind.Absolute)
        };
        var telemetryCollector = new HttpRequestTelemetryCollector();
        var transport = new TestResilientJsonTransport(httpClient, retryCount: 0, telemetryCollector);

        // Act
        var act = async () => await transport.GetAsync<Dictionary<string, string>>(new Uri("resource", UriKind.Relative), cts.Token);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*400*resource*");
        telemetryCollector.GetSummary().RequestCount.Should().Be(1);
    }

    private sealed class TestResilientJsonTransport : ResilientJsonTransport
    {
        public TestResilientJsonTransport(
            HttpClient httpClient,
            int retryCount,
            HttpRequestTelemetryCollector telemetryCollector)
            : base(httpClient, retryCount, SourceName, telemetryCollector)
        {
        }

        public const string SourceName = "Test";
    }
}
