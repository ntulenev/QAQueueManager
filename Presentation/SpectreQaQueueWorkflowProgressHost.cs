using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

using Spectre.Console;

namespace QAQueueManager.Presentation;

/// <summary>
/// Hosts QA queue workflow progress using Spectre.Console.
/// </summary>
internal sealed class SpectreQaQueueWorkflowProgressHost : IQaQueueWorkflowProgressHost
{
    /// <inheritdoc />
    public Task RunAsync(Func<IQaQueueWorkflowProgress, Task> runAsync)
    {
        ArgumentNullException.ThrowIfNull(runAsync);

        return AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            ])
            .StartAsync(context => runAsync(new SpectreQaQueueWorkflowProgress(context)));
    }

    /// <summary>
    /// Tracks and formats build and export progress for Spectre.Console.
    /// </summary>
    private sealed class SpectreQaQueueWorkflowProgress : IQaQueueWorkflowProgress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpectreQaQueueWorkflowProgress"/> class.
        /// </summary>
        /// <param name="context">The Spectre progress context.</param>
        public SpectreQaQueueWorkflowProgress(ProgressContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _jiraTask = context.AddTask("[yellow]Load QA issues from Jira[/]", autoStart: true, maxValue: 1);
            _jiraTask.IsIndeterminate = true;

            _codeTask = context.AddTask("[grey]Analyze code-linked issues (waiting for Jira)[/]", autoStart: true, maxValue: 1);
            _codeTask.IsIndeterminate = true;

            _pdfTask = context.AddTask("[grey]Export PDF (waiting for report)[/]", autoStart: true, maxValue: 2);
            _excelTask = context.AddTask("[grey]Export Excel (waiting for report)[/]", autoStart: true, maxValue: 2);
            BuildProgress = new Progress<QaQueueBuildProgress>(ReportBuildProgress);
        }

        /// <inheritdoc />
        public IProgress<QaQueueBuildProgress> BuildProgress { get; }

        /// <inheritdoc />
        public void StartPdfExport()
        {
            lock (_syncRoot)
            {
                _pdfTask.Value = 0;
                _pdfTask.Description = "[yellow]Export PDF[/] rendering document";
            }
        }

        /// <inheritdoc />
        public void ReportPdfRendered()
        {
            lock (_syncRoot)
            {
                _pdfTask.Value = 1;
                _pdfTask.Description = "[yellow]Export PDF[/] saving file";
            }
        }

        /// <inheritdoc />
        public void ReportPdfSaved(ReportFilePath path)
        {
            lock (_syncRoot)
            {
                _pdfTask.Value = 2;
                _pdfTask.Description = $"[green]Export PDF[/] {Escape(Path.GetFileName(path.Value))}";
            }
        }

        /// <inheritdoc />
        public void StartExcelExport()
        {
            lock (_syncRoot)
            {
                _excelTask.Value = 0;
                _excelTask.Description = "[yellow]Export Excel[/] rendering workbook";
            }
        }

        /// <inheritdoc />
        public void ReportExcelRendered()
        {
            lock (_syncRoot)
            {
                _excelTask.Value = 1;
                _excelTask.Description = "[yellow]Export Excel[/] saving file";
            }
        }

        /// <inheritdoc />
        public void ReportExcelSaved(ReportFilePath path)
        {
            lock (_syncRoot)
            {
                _excelTask.Value = 2;
                _excelTask.Description = $"[green]Export Excel[/] {Escape(Path.GetFileName(path.Value))}";
            }
        }

        private void ReportBuildProgress(QaQueueBuildProgress update)
        {
            ArgumentNullException.ThrowIfNull(update);

            lock (_syncRoot)
            {
                switch (update.Kind)
                {
                    case QaQueueBuildProgressKind.JiraSearchStarted:
                        _jiraTask.IsIndeterminate = true;
                        _jiraTask.Description = $"[yellow]Load QA issues from Jira[/] {FormatMessage(update.Message)}";
                        break;

                    case QaQueueBuildProgressKind.JiraSearchCompleted:
                        _jiraTask.IsIndeterminate = false;
                        _jiraTask.MaxValue = 1;
                        _jiraTask.Value = 1;
                        _jiraTask.Description = $"[green]Load QA issues from Jira[/] {FormatMessage(update.Message)}";
                        break;

                    case QaQueueBuildProgressKind.CodeAnalysisStarted:
                        if (update.Total <= 0)
                        {
                            _codeTask.IsIndeterminate = false;
                            _codeTask.MaxValue = 1;
                            _codeTask.Value = 1;
                            _codeTask.Description = "[green]Analyze code-linked issues[/] none found";
                            break;
                        }

                        _codeTask.IsIndeterminate = false;
                        _codeTask.MaxValue = update.Total;
                        _codeTask.Value = 0;
                        _codeTask.Description = $"[yellow]Analyze code-linked issues[/] {FormatMessage(update.Message)}";
                        break;

                    case QaQueueBuildProgressKind.CodeIssueStarted:
                        _codeTask.Description = FormatCodeIssueDescription(update);
                        break;

                    case QaQueueBuildProgressKind.CodeIssueCompleted:
                        _codeTask.Value = Math.Min(update.Current, (int)_codeTask.MaxValue);
                        _codeTask.Description = FormatCodeIssueDescription(update);
                        break;

                    case QaQueueBuildProgressKind.CodeAnalysisCompleted:
                        _codeTask.IsIndeterminate = false;
                        _codeTask.MaxValue = Math.Max(update.Total, 1);
                        _codeTask.Value = Math.Max(update.Total, 1);
                        _codeTask.Description = $"[green]Analyze code-linked issues[/] {FormatMessage(update.Message)}";
                        break;

                    default:
                        break;
                }
            }
        }

        private static string FormatMessage(string? message) =>
            string.IsNullOrWhiteSpace(message) ? string.Empty : Markup.Escape(" " + message);

        private static string Escape(string? value) =>
            Markup.Escape(string.IsNullOrWhiteSpace(value) ? "-" : value);

        private static string FormatCodeIssueDescription(QaQueueBuildProgress update)
        {
            var issueKey = Escape(update.IssueKey);
            return $"[yellow]Analyze code-linked issues[/] [[{update.Current}/{update.Total}]] {issueKey}";
        }

        private readonly ProgressTask _jiraTask;
        private readonly ProgressTask _codeTask;
        private readonly ProgressTask _pdfTask;
        private readonly ProgressTask _excelTask;
        private readonly Lock _syncRoot = new();
    }
}
