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
        var markupMergeSummary = new ExcelMarkupMergeSummary(
            "C:\\reports\\old",
            "C:\\reports\\old\\qa-queue-report-1.xlsx",
            ["Core|workspace/repo-a|QA-2|1.2.3"]);

        // Act
        var workflowResult = new QaQueueWorkflowResult(report, new ReportFilePath("qa-report.pdf"), new ReportFilePath("qa-report.xlsx"), markupMergeSummary);

        // Assert
        workflowResult.PdfPath.Should().Be(new ReportFilePath("qa-report.pdf"));
        workflowResult.ExcelPath.Should().Be(new ReportFilePath("qa-report.xlsx"));
        workflowResult.ExcelMarkupMergeSummary.Should().Be(markupMergeSummary);
    }
}
