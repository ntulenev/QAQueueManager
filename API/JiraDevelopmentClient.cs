using System.Globalization;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

/// <summary>
/// Loads Jira development links for pull requests and branches.
/// </summary>
internal sealed class JiraDevelopmentClient : IJiraDevelopmentClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraDevelopmentClient"/> class.
    /// </summary>
    /// <param name="transport">The Jira transport.</param>
    /// <param name="options">The Jira configuration options.</param>
    public JiraDevelopmentClient(JiraTransport transport, IOptions<JiraOptions> options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);

        _transport = transport;
        _options = options.Value;
    }

    /// <summary>
    /// Loads pull requests linked to the specified Jira issue.
    /// </summary>
    /// <param name="issueId">The Jira issue identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The linked pull requests.</returns>
    public async Task<IReadOnlyList<JiraPullRequestLink>> GetPullRequestsAsync(
        JiraIssueId issueId,
        CancellationToken cancellationToken)
    {
        var detail = await GetDetailsAsync(issueId, _options.PullRequestDataType, cancellationToken).ConfigureAwait(false);
        return detail.Count == 0
            ? []
            : [
            .. detail
                .SelectMany(static item => item.PullRequests)
                .Select(MapPullRequest)
                .Where(static item => item is not null)
                .Select(static item => item!)
                .OrderByDescending(static item => item.LastUpdatedOn ?? DateTimeOffset.MinValue)
                .ThenByDescending(static item => item.Id)
        ];
    }

    /// <summary>
    /// Loads branches linked to the specified Jira issue.
    /// </summary>
    /// <param name="issueId">The Jira issue identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The linked branches.</returns>
    public async Task<IReadOnlyList<JiraBranchLink>> GetBranchesAsync(
        JiraIssueId issueId,
        CancellationToken cancellationToken)
    {
        var detail = await GetDetailsAsync(issueId, _options.BranchDataType, cancellationToken).ConfigureAwait(false);
        return detail.Count == 0
            ? []
            : [
            .. detail
                .SelectMany(static item => item.Branches)
                .Select(MapBranch)
                .Where(static item => item is not null)
                .Select(static item => item!)
                .OrderBy(static item => item.RepositoryFullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private async Task<IReadOnlyList<JiraDevelopmentDetailDto>> GetDetailsAsync(
        JiraIssueId issueId,
        string dataType,
        CancellationToken cancellationToken)
    {
        var url =
            $"rest/dev-status/latest/issue/detail?issueId={issueId.Value.ToString(CultureInfo.InvariantCulture)}" +
            $"&applicationType={Uri.EscapeDataString(_options.BitbucketApplicationType)}" +
            $"&dataType={Uri.EscapeDataString(dataType)}";

        var response = await _transport
            .GetAsync<JiraDevelopmentDetailsResponse>(new Uri(url, UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);

        return response?.Detail ?? [];
    }

    private static JiraPullRequestLink? MapPullRequest(JiraPullRequestDto dto)
    {
        if (!int.TryParse(dto.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pullRequestId))
        {
            return null;
        }

        var repositoryFullName = NormalizeRepositoryName(dto.RepositoryName);
        if (string.IsNullOrWhiteSpace(repositoryFullName))
        {
            return null;
        }

        var updatedOn = DateTimeOffset.TryParse(
            dto.LastUpdate,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedUpdatedOn)
            ? parsedUpdatedOn
            : (DateTimeOffset?)null;

        return new JiraPullRequestLink(
            new PullRequestId(pullRequestId),
            string.IsNullOrWhiteSpace(dto.Name) ? $"PR-{pullRequestId}" : dto.Name.Trim(),
            string.IsNullOrWhiteSpace(dto.Status) ? "UNKNOWN" : dto.Status.Trim(),
            repositoryFullName,
            dto.RepositoryUrl?.Trim() ?? string.Empty,
            dto.Source?.Branch?.Trim() ?? "-",
            dto.Destination?.Branch?.Trim() ?? "-",
            dto.Url?.Trim() ?? string.Empty,
            updatedOn);
    }

    private static JiraBranchLink? MapBranch(JiraBranchDto dto)
    {
        var repositoryFullName = NormalizeRepositoryName(dto.Repository?.Name);
        return string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(repositoryFullName)
            ? null
            : new JiraBranchLink(
            dto.Name.Trim(),
            repositoryFullName,
            dto.Repository?.Url?.Trim() ?? string.Empty);
    }

    private static string NormalizeRepositoryName(string? repositoryName) => string.IsNullOrWhiteSpace(repositoryName) ? string.Empty : repositoryName.Trim().Replace('\\', '/');

    private readonly JiraTransport _transport;
    private readonly JiraOptions _options;
}
