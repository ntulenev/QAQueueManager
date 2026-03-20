using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaQueueWorkflowResultTests
{
    [Fact(DisplayName = "QaQueueWorkflowResult exposes generated report paths")]
    [Trait("Category", "Unit")]
    public void QaQueueWorkflowResultExposesGeneratedReportPaths()
    {
        // Arrange
        var report = TestData.CreateReport(groupedByTeam: true);

        // Act
        var workflowResult = new QaQueueWorkflowResult(report, new ReportFilePath("qa-report.pdf"), new ReportFilePath("qa-report.xlsx"));

        // Assert
        workflowResult.PdfPath.Should().Be(new ReportFilePath("qa-report.pdf"));
        workflowResult.ExcelPath.Should().Be(new ReportFilePath("qa-report.xlsx"));
    }
}
