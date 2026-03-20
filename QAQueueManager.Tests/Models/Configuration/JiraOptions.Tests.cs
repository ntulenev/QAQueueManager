using System.ComponentModel.DataAnnotations;

using FluentAssertions;

using QAQueueManager.Models.Configuration;

namespace QAQueueManager.Tests.Models.Configuration;

public sealed class JiraOptionsTests
{
    [Fact(DisplayName = "JiraOptions exposes expected defaults and annotations")]
    [Trait("Category", "Unit")]
    public void JiraOptionsExposesExpectedDefaultsAndAnnotations()
    {
        // Arrange
        var options = new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/", UriKind.Absolute),
            Email = "qa@example.test",
            ApiToken = "token",
            Jql = "project = QA",
        };

        // Act
        var validationResults = Validate(options);

        // Assert
        options.DevelopmentField.Should().Be("Development");
        options.TeamField.Should().BeEmpty();
        options.MaxResultsPerPage.Should().Be(100);
        options.RetryCount.Should().Be(3);
        options.BitbucketApplicationType.Should().Be("bitbucket");
        options.PullRequestDataType.Should().Be("pullrequest");
        options.BranchDataType.Should().Be("branch");
        validationResults.Should().BeEmpty();
    }

    [Fact(DisplayName = "JiraOptions validation fails when numeric ranges are invalid")]
    [Trait("Category", "Unit")]
    public void JiraOptionsValidationWhenValuesAreInvalidReturnsValidationErrors()
    {
        // Arrange
        var options = new JiraOptions
        {
            BaseUrl = new Uri("https://jira.example.test/", UriKind.Absolute),
            Email = "qa@example.test",
            ApiToken = "token",
            Jql = "project = QA",
            MaxResultsPerPage = 0,
        };

        // Act
        var validationResults = Validate(options);

        // Assert
        validationResults.Should().Contain(result => result.MemberNames.Contains(nameof(JiraOptions.MaxResultsPerPage)));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
