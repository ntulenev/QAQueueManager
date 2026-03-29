using System.Text.Json;

using FluentAssertions;

using QAQueueManager.API;

namespace QAQueueManager.Tests.API;

public sealed class JiraObjectMapperTests
{
    [Fact(DisplayName = "ExtractDisplayValue maps primitive object and array values")]
    [Trait("Category", "Unit")]
    public void ExtractDisplayValueMapsPrimitiveObjectAndArrayValues()
    {
        // Arrange
        var mapper = new JiraObjectMapper();
        var numberElement = JsonSerializer.SerializeToElement(42);
        var objectElement = JsonSerializer.SerializeToElement(new { displayName = "Core" });
        var arrayElement = JsonSerializer.SerializeToElement(new object?[]
        {
            new { value = "Alpha" },
            "Beta",
            7
        });

        // Act
        var numberValue = mapper.ExtractDisplayValue(numberElement);
        var objectValue = mapper.ExtractDisplayValue(objectElement);
        var arrayValue = mapper.ExtractDisplayValue(arrayElement);

        // Assert
        numberValue.Should().Be("42");
        objectValue.Should().Be("Core");
        arrayValue.Should().Be("Alpha, Beta, 7");
    }

    [Fact(DisplayName = "ExtractDisplayValue returns null for undefined and null values")]
    [Trait("Category", "Unit")]
    public void ExtractDisplayValueWhenElementIsUndefinedOrNullReturnsNull()
    {
        // Arrange
        var mapper = new JiraObjectMapper();
        using var document = JsonDocument.Parse("null");

        // Act
        var nullValue = mapper.ExtractDisplayValue(document.RootElement);
        var undefinedValue = mapper.ExtractDisplayValue(default);

        // Assert
        nullValue.Should().BeNull();
        undefinedValue.Should().BeNull();
    }

    [Fact(DisplayName = "ExtractDisplayValue prefers known object display properties in order")]
    [Trait("Category", "Unit")]
    public void ExtractDisplayValuePrefersKnownObjectDisplayPropertiesInOrder()
    {
        // Arrange
        var mapper = new JiraObjectMapper();
        var element = JsonSerializer.SerializeToElement(new
        {
            key = "qa-key",
            value = "qa-value",
            displayName = "QA Display",
            name = "QA Name"
        });

        // Act
        var value = mapper.ExtractDisplayValue(element);

        // Assert
        value.Should().Be("QA Name");
    }

    [Fact(DisplayName = "ExtractDisplayValue falls back to raw JSON when object has no display property")]
    [Trait("Category", "Unit")]
    public void ExtractDisplayValueWhenObjectHasNoKnownDisplayPropertyFallsBackToRawJson()
    {
        // Arrange
        var mapper = new JiraObjectMapper();
        var element = JsonSerializer.SerializeToElement(new { unexpected = "value" });

        // Act
        var value = mapper.ExtractDisplayValue(element);

        // Assert
        value.Should().Be("""{"unexpected":"value"}""");
    }
}
