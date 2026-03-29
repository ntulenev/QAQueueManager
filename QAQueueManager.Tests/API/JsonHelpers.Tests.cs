using System.Text.Json;

using FluentAssertions;

using QAQueueManager.API;

namespace QAQueueManager.Tests.API;

public sealed class JsonHelpersTests
{
    [Fact(DisplayName = "TryParseDate parses invariant date values from extracted display text")]
    [Trait("Category", "Unit")]
    public void TryParseDateParsesInvariantDateValuesFromExtractedDisplayText()
    {
        // Arrange
        var element = JsonSerializer.SerializeToElement("2026-03-20T10:15:00+00:00");

        // Act
        var value = element.TryParseDate(static json => json.GetString());

        // Assert
        value.Should().Be(new DateTimeOffset(2026, 3, 20, 10, 15, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "TryParseDate returns null when extracted display text is not a date")]
    [Trait("Category", "Unit")]
    public void TryParseDateReturnsNullWhenExtractedDisplayTextIsNotADate()
    {
        // Arrange
        var element = JsonSerializer.SerializeToElement(new { name = "not-a-date" });

        // Act
        var value = element.TryParseDate(static _ => "not-a-date");

        // Assert
        value.Should().BeNull();
    }
}
