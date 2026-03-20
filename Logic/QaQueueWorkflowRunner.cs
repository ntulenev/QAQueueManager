using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Logic;

/// <summary>
/// Executes the report build and export workflow.
/// </summary>
internal sealed class QaQueueWorkflowRunner : IQaQueueWorkflowRunner
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QaQueueWorkflowRunner"/> class.
    /// </summary>
    /// <param name="reportService">The report service.</param>
    /// <param name="pdfReportRenderer">The PDF renderer.</param>
    /// <param name="pdfReportFileStore">The PDF file store.</param>
    /// <param name="excelReportRenderer">The Excel renderer.</param>
    /// <param name="excelReportFileStore">The Excel file store.</param>
    /// <param name="reportOptions">The report configuration options.</param>
    public QaQueueWorkflowRunner(
        IQaQueueReportService reportService,
        IPdfReportRenderer pdfReportRenderer,
        IPdfReportFileStore pdfReportFileStore,
        IExcelReportRenderer excelReportRenderer,
        IExcelReportFileStore excelReportFileStore,
        IOptions<ReportOptions> reportOptions)
    {
        ArgumentNullException.ThrowIfNull(reportService);
        ArgumentNullException.ThrowIfNull(pdfReportRenderer);
        ArgumentNullException.ThrowIfNull(pdfReportFileStore);
        ArgumentNullException.ThrowIfNull(excelReportRenderer);
        ArgumentNullException.ThrowIfNull(excelReportFileStore);
        ArgumentNullException.ThrowIfNull(reportOptions);

        _reportService = reportService;
        _pdfReportRenderer = pdfReportRenderer;
        _pdfReportFileStore = pdfReportFileStore;
        _excelReportRenderer = excelReportRenderer;
        _excelReportFileStore = excelReportFileStore;
        _reportOptions = reportOptions.Value;
    }

    /// <inheritdoc />
    public async Task<QaQueueWorkflowResult> RunAsync(
        IQaQueueWorkflowProgress progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var report = await _reportService
            .BuildAsync(progress.BuildProgress, cancellationToken)
            .ConfigureAwait(false);

        progress.StartPdfExport();
        var pdfContent = _pdfReportRenderer.Render(report);
        progress.ReportPdfRendered();
        var pdfPath = _pdfReportFileStore.Save(
            pdfContent,
            new ReportFilePath(_reportOptions.PdfOutputPath));
        progress.ReportPdfSaved(pdfPath);

        progress.StartExcelExport();
        using var workbookStream = _excelReportRenderer.Render(report);
        progress.ReportExcelRendered();
        var excelPath = _excelReportFileStore.Save(
            workbookStream,
            new ReportFilePath(_reportOptions.ExcelOutputPath));
        progress.ReportExcelSaved(excelPath);

        return new QaQueueWorkflowResult(report, pdfPath, excelPath);
    }

    private readonly IQaQueueReportService _reportService;
    private readonly IPdfReportRenderer _pdfReportRenderer;
    private readonly IPdfReportFileStore _pdfReportFileStore;
    private readonly IExcelReportRenderer _excelReportRenderer;
    private readonly IExcelReportFileStore _excelReportFileStore;
    private readonly ReportOptions _reportOptions;
}
