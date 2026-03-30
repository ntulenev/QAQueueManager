using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Logic;

public sealed class QaIssueDevelopmentStateTests
{
    [Fact(DisplayName = "Parse reads direct pull request and branch counts")]
    [Trait("Category", "Unit")]
    public void ParseWhenSummaryContainsDirectCountsReturnsSnapshot()
    {
        // Act
        var snapshot = QaIssueDevelopmentState.Parse(/*lang=json,strict*/ """{"pullRequests":1,"branches":0}""");

        // Assert
        snapshot.HasSummaryPayload.Should().BeTrue();
        snapshot.HasCode.Should().BeTrue();
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
        var snapshot = QaIssueDevelopmentState.Parse(summary);

        // Assert
        snapshot.PullRequestCount.Should().Be(2);
        snapshot.BranchCount.Should().Be(3);
    }

    [Fact(DisplayName = "Parse returns unknown counts for non-json values")]
    [Trait("Category", "Unit")]
    public void ParseWhenSummaryIsNotJsonReturnsUnknownCounts()
    {
        // Act
        var snapshot = QaIssueDevelopmentState.Parse("Development");

        // Assert
        snapshot.HasSummaryPayload.Should().BeTrue();
        snapshot.HasCode.Should().BeTrue();
        snapshot.PullRequestCount.Should().BeNull();
        snapshot.BranchCount.Should().BeNull();
        snapshot.HasKnownNoDevelopment.Should().BeFalse();
    }

    [Fact(DisplayName = "Parse treats explicit zero pull requests and branches as no code")]
    [Trait("Category", "Unit")]
    public void ParseWhenSummaryReportsNoDevelopmentReturnsNoCodeState()
    {
        // Act
        var snapshot = QaIssueDevelopmentState.Parse(/*lang=json,strict*/ """{"pullRequests":0,"branches":0}""");

        // Assert
        snapshot.HasSummaryPayload.Should().BeTrue();
        snapshot.HasCode.Should().BeFalse();
        snapshot.HasKnownNoDevelopment.Should().BeTrue();
        snapshot.PullRequestCount.Should().Be(0);
        snapshot.BranchCount.Should().Be(0);
    }
}
