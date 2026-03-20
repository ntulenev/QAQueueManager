using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation;

using Spectre.Console;
using Spectre.Console.Testing;

namespace QAQueueManager.Tests.Presentation;

public sealed class SpectreQaQueueWorkflowProgressHostTests
{
    [Fact(DisplayName = "RunAsync renders workflow progress updates")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncRendersWorkflowProgressUpdates()
    {
        // Arrange
        var host = new SpectreQaQueueWorkflowProgressHost();
        var callbackCalls = 0;

        // Act
        await RunWithTestConsoleAsync(async () =>
        {
            await host.RunAsync(async progress =>
            {
                callbackCalls++;
                progress.BuildProgress.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.JiraSearchStarted, "Loading"));
                progress.BuildProgress.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.JiraSearchCompleted, "Loaded", 2, 2));
                progress.BuildProgress.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.CodeAnalysisStarted, "Analyzing", 0, 1));
                progress.BuildProgress.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.CodeIssueStarted, "Started", 1, 1, "QA-1"));
                progress.BuildProgress.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.CodeIssueCompleted, "Completed", 1, 1, "QA-1"));
                progress.BuildProgress.Report(new QaQueueBuildProgress(QaQueueBuildProgressKind.CodeAnalysisCompleted, "Done", 1, 1));
                progress.StartPdfExport();
                progress.ReportPdfRendered();
                progress.ReportPdfSaved(new ReportFilePath("exports\\qa-report.pdf"));
                progress.StartExcelExport();
                progress.ReportExcelRendered();
                progress.ReportExcelSaved(new ReportFilePath("exports\\qa-report.xlsx"));
                await Task.CompletedTask;
            });
        });

        // Assert
        callbackCalls.Should().Be(1);
    }

    private static async Task<string> RunWithTestConsoleAsync(Func<Task> action)
    {
        var original = AnsiConsole.Console;
        var console = new TestConsole();
        AnsiConsole.Console = console;

        try
        {
            await action();
            return console.Output;
        }
        finally
        {
            AnsiConsole.Console = original;
        }
    }
}
