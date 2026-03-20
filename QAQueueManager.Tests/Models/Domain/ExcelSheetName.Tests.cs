using FluentAssertions;

using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class ExcelSheetNameTests
{
    [Fact(DisplayName = "ExcelSheetName validates, sanitizes, and formats worksheet names")]
    [Trait("Category", "Unit")]
    public void ExcelSheetNameValidatesSanitizesAndFormatsWorksheetNames()
    {
        // Arrange
        var sheetName = new ExcelSheetName(" Team A ");

        // Act
        var created = ExcelSheetName.TryCreate(" Team B ", out var parsedSheetName);
        var invalidCreated = ExcelSheetName.TryCreate("Bad/Name", out var invalidSheetName);
        var sanitized = ExcelSheetName.Sanitize("  Team:/[Core]?  ");
        var fallback = ExcelSheetName.Sanitize(" /:*[]? ", "Fallback");

        // Assert
        sheetName.Value.Should().Be("Team A");
        sheetName.ToString().Should().Be("Team A");
        created.Should().BeTrue();
        parsedSheetName.Should().Be(new ExcelSheetName("Team B"));
        invalidCreated.Should().BeFalse();
        invalidSheetName.Should().Be(default(ExcelSheetName));
        sanitized.Value.Should().Be("TeamCore");
        fallback.Value.Should().Be("Fallback");
    }

    [Fact(DisplayName = "ExcelSheetName rejects blank values and sanitizes long labels down to Excel limits")]
    [Trait("Category", "Unit")]
    public void ExcelSheetNameRejectsBlankValuesAndSanitizesLongLabels()
    {
        // Arrange
        var longLabel = new string('A', 40);

        // Act
        var blankCreated = ExcelSheetName.TryCreate("   ", out var blankSheetName);
        var nullCreated = ExcelSheetName.TryCreate(null, out var nullSheetName);
        var sanitized = ExcelSheetName.Sanitize(longLabel);

        // Assert
        blankCreated.Should().BeFalse();
        blankSheetName.Should().Be(default(ExcelSheetName));
        nullCreated.Should().BeFalse();
        nullSheetName.Should().Be(default(ExcelSheetName));
        sanitized.Value.Should().Be(new string('A', 31));
    }
}
