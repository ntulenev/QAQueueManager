using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

using Spectre.Console;

namespace QAQueueManager.Logic;

/// <summary>
/// Runs the end-to-end QA queue workflow and coordinates report exports.
/// </summary>
internal sealed class QaQueueApplication : IQaQueueApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueApplication"/> class.
    /// </summary>
    /// <param name="reportService">The report service.</param>
    /// <param name="presentationService">The console presentation service.</param>
    /// <param name="pdfReportRenderer">The PDF renderer.</param>
    /// <param name="pdfReportFileStore">The PDF file store.</param>
    /// <param name="pdfReportLauncher">The PDF launcher.</param>
    /// <param name="excelReportRenderer">The Excel renderer.</param>
    /// <param name="excelReportFileStore">The Excel file store.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaQueueApplication(
        IQaQueueReportService reportService,
        IQaQueuePresentationService presentationService,
        IPdfReportRenderer pdfReportRenderer,
        IPdfReportFileStore pdfReportFileStore,
        IPdfReportLauncher pdfReportLauncher,
        IExcelReportRenderer excelReportRenderer,
        IExcelReportFileStore excelReportFileStore,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(reportService);
        ArgumentNullException.ThrowIfNull(presentationService);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        ArgumentNullException.ThrowIfNull(pdfReportFileStore);
        ArgumentNullException.ThrowIfNull(pdfReportLauncher);
        ArgumentNullException.ThrowIfNull(excelReportRenderer);
        ArgumentNullException.ThrowIfNull(excelReportFileStore);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _reportService = reportService;
        _presentationService = presentationService;
        _pdfReportRenderer = pdfReportRenderer;
        _pdfReportFileStore = pdfReportFileStore;
        _pdfReportLauncher = pdfReportLauncher;
        _excelReportRenderer = excelReportRenderer;
        _excelReportFileStore = excelReportFileStore;
        _reportOptions = reportOptions.Value;
    }

    /// <summary>
    /// Runs the full QA queue workflow and produces all configured outputs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        QaQueueReport? report = null;
        ReportFilePath? pdfPath = null;
        ReportFilePath? excelPath = null;

        await AnsiConsole.Progress()
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
            .StartAsync(async context =>
            {
                var progressView = new QaQueueProgressView(context);
                var progress = new Progress<QaQueueBuildProgress>(progressView.ReportBuildProgress);

                report = await _reportService.BuildAsync(progress, cancellationToken).ConfigureAwait(false);

                progressView.StartPdfExport();
                var content = _pdfReportRenderer.Render(report);
                progressView.ReportPdfRendered();
                pdfPath = _pdfReportFileStore.Save(content, new ReportFilePath(_reportOptions.PdfOutputPath));
                progressView.ReportPdfSaved(pdfPath ?? throw new InvalidOperationException("PDF path was not produced."));

                progressView.StartExcelExport();
                using var workbookStream = _excelReportRenderer.Render(report);
                progressView.ReportExcelRendered();
                excelPath = _excelReportFileStore.Save(workbookStream, new ReportFilePath(_reportOptions.ExcelOutputPath));
                progressView.ReportExcelSaved(excelPath ?? throw new InvalidOperationException("Excel path was not produced."));
            })
            .ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(pdfPath);
        ArgumentNullException.ThrowIfNull(excelPath);

        _presentationService.Render(report);

        Console.WriteLine();
        Console.WriteLine($"PDF exported to: {pdfPath.Value}");
        Console.WriteLine($"Excel exported to: {excelPath.Value}");

        if (_reportOptions.OpenAfterGeneration)
        {
            _pdfReportLauncher.Launch(pdfPath.Value);
        }
    }

    private readonly IQaQueueReportService _reportService;
    private readonly IQaQueuePresentationService _presentationService;
    private readonly IPdfReportRenderer _pdfReportRenderer;
    private readonly IPdfReportFileStore _pdfReportFileStore;
    private readonly IPdfReportLauncher _pdfReportLauncher;
    private readonly IExcelReportRenderer _excelReportRenderer;
    private readonly IExcelReportFileStore _excelReportFileStore;
    private readonly ReportOptions _reportOptions;

    /// <summary>
    /// Tracks and formats build and export progress for Spectre.Console.
    /// </summary>
    private sealed class QaQueueProgressView
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QaQueueProgressView"/> class.
        /// </summary>
        /// <param name="context">The Spectre progress context.</param>
        public QaQueueProgressView(ProgressContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _jiraTask = context.AddTask("[yellow]Load QA issues from Jira[/]", autoStart: true, maxValue: 1);
            _jiraTask.IsIndeterminate = true;

            _codeTask = context.AddTask("[grey]Analyze code-linked issues (waiting for Jira)[/]", autoStart: true, maxValue: 1);
            _codeTask.IsIndeterminate = true;

            _pdfTask = context.AddTask("[grey]Export PDF (waiting for report)[/]", autoStart: true, maxValue: 2);
            _excelTask = context.AddTask("[grey]Export Excel (waiting for report)[/]", autoStart: true, maxValue: 2);
        }

        /// <summary>
        /// Applies a build progress update to the console progress tasks.
        /// </summary>
        /// <param name="update">The progress update.</param>
        public void ReportBuildProgress(QaQueueBuildProgress update)
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
                }
            }
        }

        /// <summary>
        /// Marks the start of PDF export.
        /// </summary>
        public void StartPdfExport()
        {
            lock (_syncRoot)
            {
                _pdfTask.Value = 0;
                _pdfTask.Description = "[yellow]Export PDF[/] rendering document";
            }
        }

        /// <summary>
        /// Marks PDF rendering as completed.
        /// </summary>
        public void ReportPdfRendered()
        {
            lock (_syncRoot)
            {
                _pdfTask.Value = 1;
                _pdfTask.Description = "[yellow]Export PDF[/] saving file";
            }
        }

        /// <summary>
        /// Marks PDF export as completed.
        /// </summary>
        /// <param name="path">The saved PDF path.</param>
        public void ReportPdfSaved(ReportFilePath path)
        {
            lock (_syncRoot)
            {
                _pdfTask.Value = 2;
                _pdfTask.Description = $"[green]Export PDF[/] {Escape(Path.GetFileName(path.Value))}";
            }
        }

        /// <summary>
        /// Marks the start of Excel export.
        /// </summary>
        public void StartExcelExport()
        {
            lock (_syncRoot)
            {
                _excelTask.Value = 0;
                _excelTask.Description = "[yellow]Export Excel[/] rendering workbook";
            }
        }

        /// <summary>
        /// Marks Excel rendering as completed.
        /// </summary>
        public void ReportExcelRendered()
        {
            lock (_syncRoot)
            {
                _excelTask.Value = 1;
                _excelTask.Description = "[yellow]Export Excel[/] saving file";
            }
        }

        /// <summary>
        /// Marks Excel export as completed.
        /// </summary>
        /// <param name="path">The saved Excel path.</param>
        public void ReportExcelSaved(ReportFilePath path)
        {
            lock (_syncRoot)
            {
                _excelTask.Value = 2;
                _excelTask.Description = $"[green]Export Excel[/] {Escape(Path.GetFileName(path.Value))}";
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
