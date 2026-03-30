using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;
using QAQueueManager.Presentation.Pdf;
using QAQueueManager.Presentation.Shared;
using QAQueueManager.Tests.Testing;

using QuestPDF.Infrastructure;

namespace QAQueueManager.Tests.Presentation.Pdf;

public sealed class QuestPdfReportRendererTests
{
    [Fact(DisplayName = "Render creates PDF bytes for QA reports")]
    [Trait("Category", "Unit")]
    public void RenderCreatesPdfBytesForQaReports()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;
        var renderer = new QuestPdfReportRenderer(new QaQueueReportDocumentBuilder(Options.Create(new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/", UriKind.Absolute)
        })));
        var report = TestData.CreateReport(groupedByTeam: true);

        // Act
        var content = renderer.Render(report);

        // Assert
        content.Should().NotBeEmpty();
    }
}
