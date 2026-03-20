using MiniExcelLibs;

using QAQueueManager.Abstractions;
using QAQueueManager.Models.Domain;

namespace QAQueueManager.Presentation.Excel;

internal sealed class MiniExcelQaQueueReportRenderer : IExcelReportRenderer
{
    public MiniExcelQaQueueReportRenderer(
        IExcelWorkbookContentComposer workbookContentComposer,
        IWorkbookFormatter workbookFormatter)
    {
        ArgumentNullException.ThrowIfNull(workbookContentComposer);
        ArgumentNullException.ThrowIfNull(workbookFormatter);

        _workbookContentComposer = workbookContentComposer;
        _workbookFormatter = workbookFormatter;
    }

    public MemoryStream Render(QaQueueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var workbook = _workbookContentComposer.ComposeWorkbook(report);
        var outputStream = new MemoryStream();
        _ = MiniExcel.SaveAs(outputStream, workbook.Sheets, printHeader: false);
        outputStream.Position = 0;
        _workbookFormatter.Format(outputStream, workbook.Layouts);
        outputStream.Position = 0;
        return outputStream;
    }

    private readonly IExcelWorkbookContentComposer _workbookContentComposer;
    private readonly IWorkbookFormatter _workbookFormatter;
}
