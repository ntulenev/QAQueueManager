using FluentAssertions;

using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Tests.Models.Rendering;

public sealed class ExcelSheetLayoutTests
{
    [Fact(DisplayName = "ExcelSheetLayout initializes mutable collections and preserves name")]
    [Trait("Category", "Unit")]
    public void ExcelSheetLayoutInitializesMutableCollectionsAndPreservesName()
    {
        // Arrange
        var layout = new ExcelSheetLayout(new ExcelSheetName("Core Team"));

        // Act
        layout.ColumnWidths[1] = 24;
        layout.TableRanges.Add(new ExcelTableRange(2, 1, 3, 3, 4));
        layout.CellStyles["A1"] = ExcelCellStyleKind.Title;
        layout.Hyperlinks["B2"] = "https://jira.example.test/browse/QA-1";

        // Assert
        layout.Name.Should().Be(new ExcelSheetName("Core Team"));
        layout.ColumnWidths.Should().ContainKey(1).WhoseValue.Should().Be(24);
        layout.TableRanges.Should().ContainSingle();
        layout.CellStyles.Should().ContainKey("a1");
        layout.Hyperlinks.Should().ContainKey("b2");
    }
}
