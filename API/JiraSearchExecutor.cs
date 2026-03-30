using System.Net;

using QAQueueManager.Abstractions;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

/// <summary>
/// Executes Jira issue searches, including endpoint fallback and paging.
/// </summary>
internal sealed class JiraSearchExecutor : IJiraSearchExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraSearchExecutor"/> class.
    /// </summary>
    /// <param name="transport">The Jira transport.</param>
    public JiraSearchExecutor(JiraTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JiraIssueResponse>> SearchIssuesAsync(
        string jql,
        IReadOnlyList<string> requestedFields,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jql);
        ArgumentNullException.ThrowIfNull(requestedFields);

        var fields = string.Join(",", requestedFields.Distinct(StringComparer.OrdinalIgnoreCase));
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            return await SearchUsingCursorPagingAsync(jql, fields, normalizedPageSize, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return await SearchUsingStartAtPagingAsync(jql, fields, normalizedPageSize, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<JiraIssueResponse>> SearchUsingCursorPagingAsync(
        string jql,
        string requestedFields,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var issues = new List<JiraIssueResponse>();
        string? nextPageToken = null;

        while (true)
        {
            var url =
                $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}" +
                $"&fields={Uri.EscapeDataString(requestedFields)}" +
                $"&maxResults={pageSize}";

            if (!string.IsNullOrWhiteSpace(nextPageToken))
            {
                url += $"&nextPageToken={Uri.EscapeDataString(nextPageToken)}";
            }

            var page = await _transport
                .GetAsync<JiraSearchResponse>(new Uri(url, UriKind.Relative), cancellationToken)
                .ConfigureAwait(false)
                ?? new JiraSearchResponse();

            issues.AddRange(page.Issues);

            nextPageToken = page.NextPageToken;
            if (page.Issues.Count == 0 || page.IsLast || string.IsNullOrWhiteSpace(nextPageToken))
            {
                return issues;
            }
        }
    }

    private async Task<IReadOnlyList<JiraIssueResponse>> SearchUsingStartAtPagingAsync(
        string jql,
        string requestedFields,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var issues = new List<JiraIssueResponse>();
        var startAt = 0;

        while (true)
        {
            var url =
                $"rest/api/3/search?jql={Uri.EscapeDataString(jql)}" +
                $"&fields={Uri.EscapeDataString(requestedFields)}" +
                $"&startAt={startAt}&maxResults={pageSize}";

            var page = await _transport
                .GetAsync<JiraSearchResponse>(new Uri(url, UriKind.Relative), cancellationToken)
                .ConfigureAwait(false)
                ?? new JiraSearchResponse();

            issues.AddRange(page.Issues);

            if (page.Issues.Count == 0)
            {
                return issues;
            }

            startAt += page.Issues.Count;
            var total = page.Total > 0 ? page.Total : startAt;
            if (startAt >= total)
            {
                return issues;
            }
        }
    }

    private readonly JiraTransport _transport;
}
