using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Loads and resolves code details for Jira issues using Jira and Bitbucket metadata.
/// </summary>
internal sealed class QaCodeIssueDetailsLoader : IQaCodeIssueDetailsLoader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaCodeIssueDetailsLoader"/> class.
    /// </summary>
    /// <param name="repositoryResolutionBuilder">The repository resolution builder.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaCodeIssueDetailsLoader(
        IRepositoryResolutionBuilder repositoryResolutionBuilder,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(repositoryResolutionBuilder);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _repositoryResolutionBuilder = repositoryResolutionBuilder;
        _reportOptions = reportOptions.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessedCodeIssue>> LoadAsync(
        IReadOnlyList<QaIssue> issues,
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var processedIssues = new ConcurrentBag<ProcessedCodeIssue>();
        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.CodeAnalysisStarted,
            issues.Count == 0
                ? "No code-linked issues found"
                : $"Analyzing {issues.Count} code-linked issues with max parallelism {_reportOptions.MaxParallelism}",
            0,
            issues.Count));

        var startedCount = 0;
        var completedCount = 0;

        await Parallel.ForEachAsync(
            issues,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _reportOptions.MaxParallelism
            },
            async (issue, ct) =>
            {
                var started = Interlocked.Increment(ref startedCount);
                progress?.Report(new QaQueueBuildProgress(
                    QaQueueBuildProgressKind.CodeIssueStarted,
                    $"Processing {issue.Key}",
                    started,
                    issues.Count,
                    issue.Key.Value));

                var resolutions = await _repositoryResolutionBuilder.BuildAsync(issue, ct).ConfigureAwait(false);
                processedIssues.Add(new ProcessedCodeIssue(issue, resolutions));

                var completed = Interlocked.Increment(ref completedCount);
                progress?.Report(new QaQueueBuildProgress(
                    QaQueueBuildProgressKind.CodeIssueCompleted,
                    $"Processed {issue.Key}",
                    completed,
                    issues.Count,
                    issue.Key.Value));
            })
            .ConfigureAwait(false);

        progress?.Report(new QaQueueBuildProgress(
            QaQueueBuildProgressKind.CodeAnalysisCompleted,
            issues.Count == 0
                ? "Code analysis skipped: no code-linked issues"
                : $"Processed {issues.Count} code-linked issues",
            issues.Count,
            issues.Count));

        return [.. processedIssues];
    }

    private readonly IRepositoryResolutionBuilder _repositoryResolutionBuilder;
    private readonly ReportOptions _reportOptions;
}
