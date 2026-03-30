using System.Diagnostics;

using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Runs the end-to-end QA queue workflow and coordinates report exports.
/// </summary>
internal sealed class QaQueueApplication : IQaQueueApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueApplication"/> class.
    /// </summary>
    /// <param name="presentationService">The console presentation service.</param>
    /// <param name="workflowRunner">The workflow runner.</param>
    /// <param name="pdfReportLauncher">The PDF launcher.</param>
    /// <param name="workflowProgressHost">The workflow progress host.</param>
    /// <param name="requestTelemetryCollector">The HTTP request telemetry collector.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaQueueApplication(
        IQaQueuePresentationService presentationService,
        IQaQueueWorkflowRunner workflowRunner,
        IPdfReportLauncher pdfReportLauncher,
        IQaQueueWorkflowProgressHost workflowProgressHost,
        IHttpRequestTelemetryCollector requestTelemetryCollector,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(presentationService);
        ArgumentNullException.ThrowIfNull(workflowRunner);
        ArgumentNullException.ThrowIfNull(pdfReportLauncher);
        ArgumentNullException.ThrowIfNull(workflowProgressHost);
        ArgumentNullException.ThrowIfNull(requestTelemetryCollector);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _presentationService = presentationService;
        _workflowRunner = workflowRunner;
        _pdfReportLauncher = pdfReportLauncher;
        _workflowProgressHost = workflowProgressHost;
        _requestTelemetryCollector = requestTelemetryCollector;
        _reportOptions = reportOptions.Value;
    }

    /// <summary>
    /// Runs the full QA queue workflow and produces all configured outputs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _requestTelemetryCollector.Reset();
        var totalStopwatch = Stopwatch.StartNew();
        QaQueueWorkflowResult? result = null;

        try
        {
            await _workflowProgressHost.RunAsync(async progress =>
                result = await _workflowRunner.RunAsync(progress, cancellationToken).ConfigureAwait(false)
                                                ).ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(result);
            _presentationService.Render(result.Report);

            if (_reportOptions.OpenAfterGeneration)
            {
                _pdfReportLauncher.Launch(result.PdfPath);
            }
        }
        finally
        {
            totalStopwatch.Stop();
            _presentationService.RenderExecutionSummary(
                totalStopwatch.Elapsed,
                _requestTelemetryCollector.GetSummary());

            if (result is not null)
            {
                _presentationService.RenderExcelMarkupSummary(result.ExcelMarkupMergeSummary);
                _presentationService.RenderExportPaths(result.PdfPath, result.ExcelPath);
            }
        }
    }

    private readonly IQaQueuePresentationService _presentationService;
    private readonly IQaQueueWorkflowRunner _workflowRunner;
    private readonly IPdfReportLauncher _pdfReportLauncher;
    private readonly IQaQueueWorkflowProgressHost _workflowProgressHost;
    private readonly IHttpRequestTelemetryCollector _requestTelemetryCollector;
    private readonly ReportOptions _reportOptions;
}
