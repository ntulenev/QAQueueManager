using FluentAssertions;

using QAQueueManager.Logic;

namespace QAQueueManager.Tests.Logic;

public sealed class JiraDevelopmentSummaryParserTests
{
    [Fact(DisplayName = "Parse reads direct pull request and branch counts")]
    [Trait("Category", "Unit")]
    public void ParseWhenSummaryContainsDirectCountsReturnsSnapshot()
    {
        // Act
        var snapshot = JiraDevelopmentSummaryParser.Parse(/*lang=json,strict*/ """{"pullRequests":1,"branches":0}""");

        // Assert
        snapshot.PullRequestCount.Should().Be(1);
        snapshot.BranchCount.Should().Be(0);
        snapshot.HasKnownNoDevelopment.Should().BeFalse();
    }

    [Fact(DisplayName = "Parse reads nested overall counts")]
    [Trait("Category", "Unit")]
    public void ParseWhenSummaryContainsNestedCountsReturnsSnapshot()
    {
        // Act
        const string summary = /*lang=json,strict*/ """
            {"summary":{"pullrequest":{"overall":{"count":2}},"branch":{"overall":{"count":3}}}}
            """;
        var snapshot = JiraDevelopmentSummaryParser.Parse(summary);

        // Assert
        snapshot.PullRequestCount.Should().Be(2);
        snapshot.BranchCount.Should().Be(3);
    }

    [Fact(DisplayName = "Parse returns unknown counts for non-json values")]
    [Trait("Category", "Unit")]
    public void ParseWhenSummaryIsNotJsonReturnsUnknownCounts()
    {
        // Act
        var snapshot = JiraDevelopmentSummaryParser.Parse("Development");

        // Assert
        snapshot.PullRequestCount.Should().BeNull();
        snapshot.BranchCount.Should().BeNull();
        snapshot.HasKnownNoDevelopment.Should().BeFalse();
    }
}
