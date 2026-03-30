using FluentAssertions;

using QAQueueManager.Models.Rendering;
using QAQueueManager.Presentation.Excel;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class QaQueueExcelSheetBuilderTests
{
    [Fact(DisplayName = "Build creates a team worksheet with metadata, hyperlinks, and warning styles")]
    [Trait("Category", "Unit")]
    public void BuildCreatesTeamWorksheetWithMetadataHyperlinksAndWarningStyles()
    {
        var builder = new QaQueueExcelSheetBuilder(new Uri("https://jira.example.test/browse/", UriKind.Absolute));
        var report = TestData.CreateReport(groupedByTeam: true);
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var builtSheet = builder.Build(report, report.Teams[0], usedSheetNames);

        builtSheet.Name.Value.Should().Be("Core");
        builtSheet.Layout.HiddenColumns.Should().Contain(13);
        builtSheet.Layout.Hyperlinks.Should().ContainValue("https://jira.example.test/browse/QA-1");
        builtSheet.Layout.CellStyles.Should().ContainValue(ExcelCellStyleKind.Warning);
        builtSheet.Layout.TableRanges.Should().NotBeEmpty();
        builtSheet.Rows.Any(static row => row.TryGetValue("C4", out var value) && Equals(value, "Assignee")).Should().BeTrue();
        builtSheet.Rows.Any(static row => row.TryGetValue("C13", out var value) && Equals(value, "MarkupKey")).Should().BeTrue();
        builtSheet.Rows.Any(static row => row.TryGetValue("C13", out var value) && value is string markupKey && markupKey.Contains("Core|workspace/repo-a|QA-2|1.2.3", StringComparison.Ordinal)).Should().BeTrue();
    }
}
