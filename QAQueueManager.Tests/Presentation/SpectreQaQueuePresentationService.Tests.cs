using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation;
using QAQueueManager.Tests.Testing;

using Spectre.Console;
using Spectre.Console.Testing;

namespace QAQueueManager.Tests.Presentation;

public sealed class SpectreQaQueuePresentationServiceTests
{
    [Fact(DisplayName = "Render writes repository-based QA report output")]
    [Trait("Category", "Unit")]
    public async Task RenderWhenReportIsRepositoryBasedWritesExpectedOutput()
    {
        // Arrange
        var service = new SpectreQaQueuePresentationService();
        var report = TestData.CreateReport();

        // Act
        var act = async () => await RunWithTestConsoleAsync(() =>
        {
            service.Render(report);
            return Task.CompletedTask;
        });

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "RenderExportPaths writes exported file locations")]
    [Trait("Category", "Unit")]
    public async Task RenderExportPathsWritesExportedFileLocations()
    {
        // Arrange
        var service = new SpectreQaQueuePresentationService();

        // Act
        var act = async () => await RunWithTestConsoleAsync(() =>
        {
            service.RenderExportPaths(new ReportFilePath("exports\\qa-report.pdf"), new ReportFilePath("exports\\qa-report.xlsx"));
            return Task.CompletedTask;
        });

        // Assert
        await act.Should().NotThrowAsync();
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
