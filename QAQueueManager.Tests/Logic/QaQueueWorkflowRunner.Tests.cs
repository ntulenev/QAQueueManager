using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using QAQueueManager.Abstractions;
using QAQueueManager.Logic;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Logic;

public sealed class QaQueueWorkflowRunnerTests
{
    [Fact(DisplayName = "RunAsync builds report, exports files, and reports workflow milestones")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncBuildsReportExportsFilesAndReportsWorkflowMilestones()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var report = TestData.CreateReport();
        var pdfContent = new byte[] { 1, 2, 3 };
        using var excelContent = new MemoryStream([4, 5, 6]);
        var workflowEvents = new List<string>();
        var progress = new Mock<IQaQueueWorkflowProgress>(MockBehavior.Strict);
        progress.SetupGet(p => p.BuildProgress)
            .Returns(new Progress<QaQueueBuildProgress>(_ => { }));
        progress.Setup(p => p.StartPdfExport()).Callback(() => workflowEvents.Add("StartPdf"));
        progress.Setup(p => p.ReportPdfRendered()).Callback(() => workflowEvents.Add("PdfRendered"));
        progress.Setup(p => p.ReportPdfSaved(It.Is<ReportFilePath>(path => path == new ReportFilePath("exports\\qa-report.pdf"))))
            .Callback(() => workflowEvents.Add("PdfSaved"));
        progress.Setup(p => p.StartExcelExport()).Callback(() => workflowEvents.Add("StartExcel"));
        progress.Setup(p => p.ReportExcelRendered()).Callback(() => workflowEvents.Add("ExcelRendered"));
        progress.Setup(p => p.ReportExcelSaved(It.Is<ReportFilePath>(path => path == new ReportFilePath("exports\\qa-report.xlsx"))))
            .Callback(() => workflowEvents.Add("ExcelSaved"));

        var reportService = new Mock<IQaQueueReportService>(MockBehavior.Strict);
        reportService
            .Setup(service => service.BuildAsync(
                It.Is<IProgress<QaQueueBuildProgress>>(buildProgress => buildProgress == progress.Object.BuildProgress),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => workflowEvents.Add("Build"))
            .ReturnsAsync(report);

        var pdfReportRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfReportRenderer
            .Setup(renderer => renderer.Render(It.Is<QaQueueReport>(value => value == report)))
            .Callback(() => workflowEvents.Add("RenderPdf"))
            .Returns(pdfContent);

        var pdfReportFileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        pdfReportFileStore
            .Setup(store => store.Save(
                It.Is<byte[]>(content => content.SequenceEqual(pdfContent)),
                It.Is<ReportFilePath>(path => path == new ReportFilePath("qa-report.pdf"))))
            .Callback(() => workflowEvents.Add("SavePdf"))
            .Returns(new ReportFilePath("exports\\qa-report.pdf"));

        var excelReportRenderer = new Mock<IExcelReportRenderer>(MockBehavior.Strict);
        excelReportRenderer
            .Setup(renderer => renderer.Render(It.Is<QaQueueReport>(value => value == report)))
            .Callback(() => workflowEvents.Add("RenderExcel"))
            .Returns(excelContent);

        var excelReportFileStore = new Mock<IExcelReportFileStore>(MockBehavior.Strict);
        excelReportFileStore
            .Setup(store => store.Save(
                It.Is<Stream>(stream => stream == excelContent),
                It.Is<ReportFilePath>(path => path == new ReportFilePath("qa-report.xlsx"))))
            .Callback(() => workflowEvents.Add("SaveExcel"))
            .Returns(new ReportFilePath("exports\\qa-report.xlsx"));

        var runner = new QaQueueWorkflowRunner(
            reportService.Object,
            pdfReportRenderer.Object,
            pdfReportFileStore.Object,
            excelReportRenderer.Object,
            excelReportFileStore.Object,
            Options.Create(new ReportOptions
            {
                PdfOutputPath = "qa-report.pdf",
                ExcelOutputPath = "qa-report.xlsx"
            }));

        // Act
        var result = await runner.RunAsync(progress.Object, cts.Token);

        // Assert
        result.Report.Should().Be(report);
        result.PdfPath.Should().Be(new ReportFilePath("exports\\qa-report.pdf"));
        result.ExcelPath.Should().Be(new ReportFilePath("exports\\qa-report.xlsx"));
        workflowEvents.Should().ContainInOrder(
            "Build",
            "StartPdf",
            "RenderPdf",
            "PdfRendered",
            "SavePdf",
            "PdfSaved",
            "StartExcel",
            "RenderExcel",
            "ExcelRendered",
            "SaveExcel",
            "ExcelSaved");
    }
}
