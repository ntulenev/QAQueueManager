using System.Net;

using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;
using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Transport;

public sealed class JiraTransportTests
{
    [Fact(DisplayName = "GetAsync deserializes successful Jira responses")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsSuccessfulDeserializesPayload()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            request.RequestUri.Should().Be(new Uri("https://jira.example.test/rest/api/3/field", UriKind.Absolute));
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new Dictionary<string, string> { ["value"] = "ok" }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));

        // Act
        var response = await transport.GetAsync<Dictionary<string, string>>(new Uri("rest/api/3/field", UriKind.Relative), cts.Token);

        // Assert
        response.Should().NotBeNull();
        response!["value"].Should().Be("ok");
        handler.SendCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GetAsync retries retriable Jira responses before succeeding")]
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
                ? RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{}""", HttpStatusCode.InternalServerError)
                : RecordingHttpMessageHandler.CreateJsonResponse(new Dictionary<string, string> { ["value"] = "retried" }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 1 }));

        // Act
        var response = await transport.GetAsync<Dictionary<string, string>>(new Uri("rest/api/3/field", UriKind.Relative), cts.Token);

        // Assert
        response.Should().NotBeNull();
        response!["value"].Should().Be("retried");
        sendCalls.Should().Be(2);
    }

    [Fact(DisplayName = "GetAsync throws for non-retriable Jira failures")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncWhenResponseIsNonRetriableThrowsHttpRequestException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{"error":"forbidden"}""", HttpStatusCode.BadRequest)));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 }));

        // Act
        var act = async () => await transport.GetAsync<Dictionary<string, string>>(new Uri("rest/api/3/field", UriKind.Relative), cts.Token);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*400*rest/api/3/field*");
    }
}
