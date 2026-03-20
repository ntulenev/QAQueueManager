using System.Net;
using System.Text;
using System.Text.Json;

namespace QAQueueManager.Tests.Testing;

internal sealed class RecordingHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    : HttpMessageHandler
{
    public List<Uri?> RequestUris { get; } = [];

    public int SendCalls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SendCalls++;
        RequestUris.Add(request.RequestUri);
        return await handler(request, cancellationToken);
    }

    public static HttpResponseMessage CreateJsonResponse(object? payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = payload is string raw
            ? raw
            : JsonSerializer.Serialize(payload);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
