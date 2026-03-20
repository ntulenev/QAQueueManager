using FluentAssertions;

using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Tests.Models.Rendering;

public sealed class ExcelTableRangeTests
{
    [Fact(DisplayName = "ExcelTableRange exposes supplied coordinates")]
    [Trait("Category", "Unit")]
    public void ExcelTableRangeExposesSuppliedCoordinates()
    {
        // Arrange
        var tableRange = new ExcelTableRange(2, 1, 5, 3, 10);

        // Assert
        tableRange.HeaderRow.Should().Be(2);
        tableRange.StartColumnIndex.Should().Be(1);
        tableRange.EndColumnIndex.Should().Be(5);
        tableRange.DataStartRow.Should().Be(3);
        tableRange.DataEndRow.Should().Be(10);
    }
}
