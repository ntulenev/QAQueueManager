using MiniExcelLibs;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation.Excel;

/// <summary>
/// Renders the QA queue report to an Excel workbook using MiniExcel and OpenXML formatting.
/// </summary>
internal sealed class MiniExcelQaQueueReportRenderer : IExcelReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiniExcelQaQueueReportRenderer"/> class.
    /// </summary>
    /// <param name="workbookContentComposer">The workbook content composer.</param>
    /// <param name="workbookFormatter">The workbook formatter.</param>
    /// <param name="markupMergeService">The Excel markup merge service.</param>
    public MiniExcelQaQueueReportRenderer(
        IExcelWorkbookContentComposer workbookContentComposer,
        IWorkbookFormatter workbookFormatter,
        IExcelMarkupMergeService markupMergeService)
    {
        ArgumentNullException.ThrowIfNull(workbookContentComposer);
        ArgumentNullException.ThrowIfNull(workbookFormatter);
        ArgumentNullException.ThrowIfNull(markupMergeService);

        _workbookContentComposer = workbookContentComposer;
        _workbookFormatter = workbookFormatter;
        _markupMergeService = markupMergeService;
    }

    /// <summary>
    /// Renders the supplied report into an in-memory Excel workbook.
    /// </summary>
    /// <param name="report">The report to render.</param>
    /// <returns>The generated workbook stream and formatter metadata.</returns>
    public ExcelRenderResult Render(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var workbook = _workbookContentComposer.ComposeWorkbook(report);
        var outputStream = new MemoryStream();
        _ = MiniExcel.SaveAs(
            outputStream,
            workbook.Sheets.ToDictionary(static pair => pair.Key.Value, static pair => pair.Value, StringComparer.Ordinal),
            printHeader: false);
        outputStream.Position = 0;
        _workbookFormatter.Format(outputStream, workbook.Layouts);
        var markupMergeSummary = _markupMergeService.Merge(outputStream, workbook.Layouts);
        outputStream.Position = 0;
        return new ExcelRenderResult(outputStream, markupMergeSummary);
    }

    private readonly IExcelWorkbookContentComposer _workbookContentComposer;
    private readonly IWorkbookFormatter _workbookFormatter;
    private readonly IExcelMarkupMergeService _markupMergeService;
}
