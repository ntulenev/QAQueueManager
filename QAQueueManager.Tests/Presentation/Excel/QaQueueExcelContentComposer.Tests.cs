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
        var rows = workbook.Sheets[sheetName].Should().BeAssignableTo<List<Dictionary<string, object?>>>().Subject;
        rows.Any(static row => row.TryGetValue("C4", out var value) && Equals(value, "Assignee")).Should().BeTrue();
        rows.Any(static row => row.TryGetValue("C4", out var value) && Equals(value, "QA Engineer")).Should().BeTrue();
        rows.Any(static row => row.TryGetValue("C13", out var value) && Equals(value, "MarkupKey")).Should().BeTrue();
        rows.Any(static row => row.TryGetValue("C13", out var value) && value is string markupKey && markupKey.Contains("Core|workspace/repo-a|QA-2|1.2.3", StringComparison.Ordinal)).Should().BeTrue();
        layout.HiddenColumns.Should().Contain(13);
        layout.Hyperlinks.Should().ContainValue("https://jira.example.test/browse/QA-1");
        layout.CellStyles.Should().ContainValue(ExcelCellStyleKind.Warning);
        layout.TableRanges.Should().NotBeEmpty();
    }
}
