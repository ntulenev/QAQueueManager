using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using QAQueueManager.Abstractions;
using QAQueueManager.Logic;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
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
        var result = new QaQueueWorkflowResult(report, new ReportFilePath("exports\\qa-report.pdf"), new ReportFilePath("exports\\qa-report.xlsx"));
        var launchCalls = 0;
        var workflowEvents = new List<string>();
        var workflowProgress = new Mock<IQaQueueWorkflowProgress>(MockBehavior.Strict);

        var presentationService = new Mock<IQaQueuePresentationService>(MockBehavior.Strict);
        presentationService
            .Setup(service => service.Render(It.Is<QaQueueReport>(value => value == report)))
            .Callback(() => workflowEvents.Add("RenderReport"));
        presentationService
            .Setup(service => service.RenderExportPaths(
                It.Is<ReportFilePath>(path => path == result.PdfPath),
                It.Is<ReportFilePath>(path => path == result.ExcelPath)))
            .Callback(() => workflowEvents.Add("RenderPaths"));

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
            Options.Create(new ReportOptions { OpenAfterGeneration = true }));

        // Act
        await application.RunAsync(cts.Token);

        // Assert
        launchCalls.Should().Be(1);
        workflowEvents.Should().ContainInOrder("RunHost", "RunWorkflow", "RenderReport", "RenderPaths", "LaunchPdf");
    }

    [Fact(DisplayName = "RunAsync skips PDF launch when automatic opening is disabled")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncWhenOpenAfterGenerationIsDisabledSkipsPdfLaunch()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var report = TestData.CreateReport();
        var result = new QaQueueWorkflowResult(report, new ReportFilePath("exports\\qa-report.pdf"), new ReportFilePath("exports\\qa-report.xlsx"));
        var launchCalls = 0;
        var workflowProgress = new Mock<IQaQueueWorkflowProgress>(MockBehavior.Strict);

        var presentationService = new Mock<IQaQueuePresentationService>(MockBehavior.Strict);
        presentationService.Setup(service => service.Render(It.Is<QaQueueReport>(value => value == report))).Callback(() => { });
        presentationService.Setup(service => service.RenderExportPaths(
            It.Is<ReportFilePath>(path => path == result.PdfPath),
            It.Is<ReportFilePath>(path => path == result.ExcelPath))).Callback(() => { });

        var workflowRunner = new Mock<IQaQueueWorkflowRunner>(MockBehavior.Strict);
        workflowRunner
            .Setup(runner => runner.RunAsync(
                It.Is<IQaQueueWorkflowProgress>(progress => progress == workflowProgress.Object),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback(() => { })
            .ReturnsAsync(result);

        var pdfReportLauncher = new Mock<IPdfReportLauncher>(MockBehavior.Strict);

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
            Options.Create(new ReportOptions { OpenAfterGeneration = false }));

        // Act
        await application.RunAsync(cts.Token);

        // Assert
        launchCalls.Should().Be(0);
    }
}
