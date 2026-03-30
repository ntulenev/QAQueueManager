using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Builds the QA queue report from Jira issues and Bitbucket metadata.
/// </summary>
internal sealed class QaQueueReportService : IQaQueueReportService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueReportService"/> class.
    /// </summary>
    /// <param name="jiraIssueSearchClient">The Jira issue search client.</param>
    /// <param name="codeIssueDetailsLoader">The code issue details loader.</param>
    /// <param name="reportBuilder">The report builder.</param>
    public QaQueueReportService(
        IJiraIssueSearchClient jiraIssueSearchClient,
        IQaCodeIssueDetailsLoader codeIssueDetailsLoader,
        IQaQueueReportBuilder reportBuilder)
    {
        ArgumentNullException.ThrowIfNull(jiraIssueSearchClient);
        ArgumentNullException.ThrowIfNull(codeIssueDetailsLoader);
        ArgumentNullException.ThrowIfNull(reportBuilder);

        _jiraIssueSearchClient = jiraIssueSearchClient;
        _codeIssueDetailsLoader = codeIssueDetailsLoader;
        _reportBuilder = reportBuilder;
    }

    /// <summary>
    /// Builds the QA queue report and emits optional progress updates.
    /// </summary>
    /// <param name="progress">The optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The generated QA queue report.</returns>
    public async Task<QaQueueReport> BuildAsync(
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.JiraSearchStarted,
            "Loading issues from Jira"));

        var allIssues = await _jiraIssueSearchClient.SearchIssuesAsync(cancellationToken).ConfigureAwait(false);
        var codeIssues = allIssues
            .Where(static issue => issue.HasCode)
            .ToList();

        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.JiraSearchCompleted,
            $"Found {allIssues.Count} QA issues, {codeIssues.Count} with code",
            allIssues.Count,
            allIssues.Count));

        var noCodeIssues = allIssues
            .Where(static issue => !issue.HasCode)
            .OrderBy(static issue => issue.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var processedIssues = await _codeIssueDetailsLoader
            .LoadAsync(codeIssues, progress, cancellationToken)
            .ConfigureAwait(false);

        return _reportBuilder.Build(noCodeIssues, processedIssues);
    }

    private readonly IJiraIssueSearchClient _jiraIssueSearchClient;
    private readonly IQaCodeIssueDetailsLoader _codeIssueDetailsLoader;
    private readonly IQaQueueReportBuilder _reportBuilder;
}
