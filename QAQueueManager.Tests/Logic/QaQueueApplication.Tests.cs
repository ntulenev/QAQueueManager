using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using QAQueueManager.Abstractions;
using QAQueueManager.Logic;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Telemetry;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Logic;

public sealed class QaQueueApplicationTests
{
    [Fact(DisplayName = "RunAsync renders report output and launches PDF when configured")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenOpenAfterGenerationIsEnabledRendersOutputAndLaunchesPdf()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var report = TestData.CreateReport();
        var markupMergeSummary = new ExcelMarkupMergeSummary(
            "C:\\reports\\old",
            "C:\\reports\\old\\qa-queue-report-1.xlsx",
            ["Core|workspace/repo-a|QA-2|1.2.3"]);
        var result = new QaQueueWorkflowResult(report, new ReportFilePath("exports\\qa-report.pdf"), new ReportFilePath("exports\\qa-report.xlsx"), markupMergeSummary);
        var launchCalls = 0;
        var workflowEvents = new List<string>();
        var workflowProgress = new Mock<IQaQueueWorkflowProgress>(MockBehavior.Strict);
        var telemetrySummary = new HttpRequestTelemetrySummary(0, 0, 0, TimeSpan.Zero, []);

        var presentationService = new Mock<IQaQueuePresentationService>(MockBehavior.Strict);
        presentationService
            .Setup(service => service.Render(It.Is<QaQueueReport>(value => value == report)))
            .Callback(() => workflowEvents.Add("RenderReport"));
        presentationService
            .Setup(service => service.RenderExportPaths(
                It.Is<ReportFilePath>(path => path == result.PdfPath),
                It.Is<ReportFilePath>(path => path == result.ExcelPath)))
            .Callback(() => workflowEvents.Add("RenderPaths"));
        presentationService
            .Setup(service => service.RenderExecutionSummary(
                It.IsAny<TimeSpan>(),
                It.Is<HttpRequestTelemetrySummary>(value => value == telemetrySummary)))
            .Callback(() => workflowEvents.Add("RenderTelemetry"));
        presentationService
            .Setup(service => service.RenderExcelMarkupSummary(It.Is<ExcelMarkupMergeSummary>(value => value == markupMergeSummary)))
            .Callback(() => workflowEvents.Add("RenderExcelMarkup"));

        var workflowRunner = new Mock<IQaQueueWorkflowRunner>(MockBehavior.Strict);
        workflowRunner
            .Setup(runner => runner.RunAsync(
                It.Is<IQaQueueWorkflowProgress>(progress => progress == workflowProgress.Object),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => workflowEvents.Add("RunWorkflow"))
            .ReturnsAsync(result);

        var pdfReportLauncher = new Mock<IPdfReportLauncher>(MockBehavior.Strict);
        pdfReportLauncher
            .Setup(launcher => launcher.Launch(It.Is<ReportFilePath>(path => path == result.PdfPath)))
            .Callback(() =>
            {
                launchCalls++;
                workflowEvents.Add("LaunchPdf");
            });

        var requestTelemetryCollector = new Mock<IHttpRequestTelemetryCollector>(MockBehavior.Strict);
        requestTelemetryCollector
            .Setup(collector => collector.Reset())
            .Callback(() => workflowEvents.Add("ResetTelemetry"));
        requestTelemetryCollector
            .Setup(collector => collector.GetSummary())
            .Returns(telemetrySummary);

        var workflowProgressHost = new Mock<IQaQueueWorkflowProgressHost>(MockBehavior.Strict);
        workflowProgressHost
            .Setup(host => host.RunAsync(It.Is<Func<IQaQueueWorkflowProgress, Task>>(callback => callback != null)))
            .Callback<Func<IQaQueueWorkflowProgress, Task>>(callback =>
            {
                workflowEvents.Add("RunHost");
                callback(workflowProgress.Object).GetAwaiter().GetResult();
            })
            .Returns(Task.CompletedTask);

        var application = new QaQueueApplication(
            presentationService.Object,
            workflowRunner.Object,
            pdfReportLauncher.Object,
            workflowProgressHost.Object,
            requestTelemetryCollector.Object,
            Options.Create(new ReportOptions { OpenAfterGeneration = true }));

        // Act
        await application.RunAsync(cts.Token);

        // Assert
        launchCalls.Should().Be(1);
        workflowEvents.Should().ContainInOrder(
            "ResetTelemetry",
            "RunHost",
            "RunWorkflow",
            "RenderReport",
            "LaunchPdf",
            "RenderTelemetry",
            "RenderExcelMarkup",
            "RenderPaths");
    }

    [Fact(DisplayName = "RunAsync skips PDF launch when automatic opening is disabled")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenOpenAfterGenerationIsDisabledSkipsPdfLaunch()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var report = TestData.CreateReport();
        var markupMergeSummary = new ExcelMarkupMergeSummary(null, null, []);
        var result = new QaQueueWorkflowResult(report, new ReportFilePath("exports\\qa-report.pdf"), new ReportFilePath("exports\\qa-report.xlsx"), markupMergeSummary);
        var launchCalls = 0;
        var workflowProgress = new Mock<IQaQueueWorkflowProgress>(MockBehavior.Strict);
        var telemetrySummary = new HttpRequestTelemetrySummary(0, 0, 0, TimeSpan.Zero, []);

        var presentationService = new Mock<IQaQueuePresentationService>(MockBehavior.Strict);
        presentationService.Setup(service => service.Render(It.Is<QaQueueReport>(value => value == report))).Callback(() => { });
        presentationService.Setup(service => service.RenderExportPaths(
            It.Is<ReportFilePath>(path => path == result.PdfPath),
            It.Is<ReportFilePath>(path => path == result.ExcelPath))).Callback(() => { });
        presentationService.Setup(service => service.RenderExecutionSummary(
            It.IsAny<TimeSpan>(),
            It.Is<HttpRequestTelemetrySummary>(value => value == telemetrySummary))).Callback(() => { });
        presentationService.Setup(service => service.RenderExcelMarkupSummary(
            It.Is<ExcelMarkupMergeSummary>(value => value == markupMergeSummary))).Callback(() => { });

        var workflowRunner = new Mock<IQaQueueWorkflowRunner>(MockBehavior.Strict);
        workflowRunner
            .Setup(runner => runner.RunAsync(
                It.Is<IQaQueueWorkflowProgress>(progress => progress == workflowProgress.Object),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => { })
            .ReturnsAsync(result);

        var pdfReportLauncher = new Mock<IPdfReportLauncher>(MockBehavior.Strict);

        var requestTelemetryCollector = new Mock<IHttpRequestTelemetryCollector>(MockBehavior.Strict);
        requestTelemetryCollector.Setup(collector => collector.Reset()).Callback(() => { });
        requestTelemetryCollector.Setup(collector => collector.GetSummary()).Returns(telemetrySummary);

        var workflowProgressHost = new Mock<IQaQueueWorkflowProgressHost>(MockBehavior.Strict);
        workflowProgressHost
            .Setup(host => host.RunAsync(It.Is<Func<IQaQueueWorkflowProgress, Task>>(callback => callback != null)))
            .Callback<Func<IQaQueueWorkflowProgress, Task>>(callback => callback(workflowProgress.Object).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        var application = new QaQueueApplication(
            presentationService.Object,
            workflowRunner.Object,
            pdfReportLauncher.Object,
            workflowProgressHost.Object,
            requestTelemetryCollector.Object,
            Options.Create(new ReportOptions { OpenAfterGeneration = false }));

        // Act
        await application.RunAsync(cts.Token);

        // Assert
        launchCalls.Should().Be(0);
    }
}
