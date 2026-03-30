using System.ComponentModel.DataAnnotations;

using FluentAssertions;

using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Tests.Models.Configuration;

public sealed class ReportOptionsTests
{
    [Fact(DisplayName = "ReportOptions exposes expected defaults and annotations")]
    [Trait("Category", "Unit")]
    public void ReportOptionsExposesExpectedDefaultsAndAnnotations()
    {
        // Arrange
        var options = new ReportOptions
        {
            Title = "QA Queue",
            TargetBranch = "main",
        };

        // Act
        var validationResults = Validate(options);

        // Assert
        options.PdfOutputPath.Should().Be("qa-queue-report.pdf");
        options.ExcelOutputPath.Should().Be("qa-queue-report.xlsx");
        options.OldReportsPath.Should().BeNull();
        options.MaxParallelism.Should().Be(4);
        options.HideNoCodeIssues.Should().BeFalse();
        options.OpenAfterGeneration.Should().BeFalse();
        validationResults.Should().BeEmpty();
    }

    [Fact(DisplayName = "ReportOptions validation fails when numeric ranges are invalid")]
    [Trait("Category", "Unit")]
    public void ReportOptionsValidationWhenValuesAreInvalidReturnsValidationErrors()
    {
        // Arrange
        var options = new ReportOptions
        {
            Title = "QA Queue",
            TargetBranch = "main",
            MaxParallelism = 33,
        };

        // Act
        var validationResults = Validate(options);

        // Assert
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(ReportOptions.MaxParallelism)));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
