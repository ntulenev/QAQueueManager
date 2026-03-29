using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Telemetry;
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

    [Fact(DisplayName = "RenderExecutionSummary writes HTTP telemetry output")]
    [Trait("Category", "Unit")]
    public async Task RenderExecutionSummaryWritesTelemetryOutput()
    {
        // Arrange
        var service = new SpectreQaQueuePresentationService();
        var telemetry = new HttpRequestTelemetrySummary(
            RequestCount: 3,
            RetryCount: 1,
            ResponseBytes: 2048,
            TotalDuration: TimeSpan.FromSeconds(1.5),
            Endpoints:
            [
                new HttpRequestTelemetryEndpointSummary(
                    Source: "Jira",
                    Method: "GET",
                    Endpoint: "/rest/api/3/search",
                    RequestCount: 2,
                    RetryCount: 1,
                    ResponseBytes: 1536,
                    TotalDuration: TimeSpan.FromSeconds(1.2),
                    MaxDuration: TimeSpan.FromSeconds(0.8)),
                new HttpRequestTelemetryEndpointSummary(
                    Source: "Bitbucket",
                    Method: "GET",
                    Endpoint: "/repositories/ws/repo-a/pullrequests/1",
                    RequestCount: 1,
                    RetryCount: 0,
                    ResponseBytes: 512,
                    TotalDuration: TimeSpan.FromSeconds(0.3),
                    MaxDuration: TimeSpan.FromSeconds(0.3))
            ]);

        // Act
        var output = await RunWithTestConsoleAsync(() =>
        {
            service.RenderExecutionSummary(TimeSpan.FromSeconds(2), telemetry);
            return Task.CompletedTask;
        });

        // Assert
        output.Should().Contain("HTTP telemetry");
        output.Should().Contain("Requests: 3");
        output.Should().Contain("512 B");
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
