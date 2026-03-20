using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Rendering;
using QAQueueManager.Presentation.Excel;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class QaQueueExcelContentComposerTests
{
    [Fact(DisplayName = "ComposeWorkbook builds team sheets, hyperlinks, and warning styles")]
    [Trait("Category", "Unit")]
    public void ComposeWorkbookBuildsTeamSheetsHyperlinksAndWarningStyles()
    {
        // Arrange
        var composer = new QaQueueExcelContentComposer(Options.Create(new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/", UriKind.Absolute)
        }));
        var report = TestData.CreateReport(groupedByTeam: true);

        // Act
        var workbook = composer.ComposeWorkbook(report);

        // Assert
        workbook.Sheets.Should().ContainSingle();
        workbook.Layouts.Should().ContainSingle();
        var sheetName = workbook.Sheets.Keys.Single();
        sheetName.Value.Should().Be("Core");
        var layout = workbook.Layouts[sheetName];
        layout.Hyperlinks.Should().ContainValue("https://jira.example.test/browse/QA-1");
        layout.CellStyles.Should().ContainValue(ExcelCellStyleKind.Warning);
        layout.TableRanges.Should().NotBeEmpty();
    }
}
