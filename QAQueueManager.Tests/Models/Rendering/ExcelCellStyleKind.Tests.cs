using FluentAssertions;

using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Tests.Models.Rendering;

public sealed class ExcelCellStyleKindTests
{
    [Fact(DisplayName = "ExcelCellStyleKind warning value remains stable")]
    [Trait("Category", "Unit")]
    public void ExcelCellStyleKindWarningValueRemainsStable()
    {
        // Assert
        ((int)ExcelCellStyleKind.Warning).Should().Be(8);
    }
}
