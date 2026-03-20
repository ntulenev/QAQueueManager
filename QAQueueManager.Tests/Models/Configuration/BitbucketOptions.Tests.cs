using System.ComponentModel.DataAnnotations;

using FluentAssertions;

using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Tests.Models.Configuration;

public sealed class BitbucketOptionsTests
{
    [Fact(DisplayName = "BitbucketOptions exposes expected defaults and annotations")]
    [Trait("Category", "Unit")]
    public void BitbucketOptionsExposesExpectedDefaultsAndAnnotations()
    {
        // Arrange
        var options = new BitbucketOptions
        {
            BaseUrl = new Uri("https://bitbucket.example.test/", UriKind.Absolute),
            Workspace = "workspace",
            AuthEmail = "qa@example.test",
            AuthApiToken = "token",
        };

        // Act
        var validationResults = Validate(options);

        // Assert
        options.RetryCount.Should().Be(3);
        validationResults.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketOptions validation fails when required values are missing")]
    [Trait("Category", "Unit")]
    public void BitbucketOptionsValidationWhenValuesAreInvalidReturnsValidationErrors()
    {
        // Arrange
        var options = new BitbucketOptions();

        // Act
        var validationResults = Validate(options);

        // Assert
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(BitbucketOptions.BaseUrl)));
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(BitbucketOptions.Workspace)));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
