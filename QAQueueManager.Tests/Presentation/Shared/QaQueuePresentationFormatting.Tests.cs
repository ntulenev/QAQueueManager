using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation.Shared;
using QAQueueManager.Tests.Testing;

#pragma warning disable CA1716
namespace QAQueueManager.Tests.Presentation.Shared;
#pragma warning restore CA1716

public sealed class QaQueuePresentationFormattingTests
{
    [Fact(DisplayName = "FormatReportTimestamp formats the full report timestamp")]
    [Trait("Category", "Unit")]
    public void FormatReportTimestampFormatsFullReportTimestamp()
    {
        var value = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);

        var formatted = QaQueuePresentationFormatting.FormatReportTimestamp(value);

        formatted.Should().Be("2026-03-20 10:00:00 +00:00");
    }

    [Fact(DisplayName = "FormatIssueTimestamp formats issue timestamp and handles null")]
    [Trait("Category", "Unit")]
    public void FormatIssueTimestampFormatsIssueTimestampAndHandlesNull()
    {
        var value = new DateTimeOffset(2026, 3, 20, 9, 30, 0, TimeSpan.Zero);

        QaQueuePresentationFormatting.FormatIssueTimestamp(value).Should().Be("2026-03-20 09:30");
        QaQueuePresentationFormatting.FormatIssueTimestamp(null).Should().Be("-");
    }

    [Fact(DisplayName = "FormatPullRequests formats Jira pull requests")]
    [Trait("Category", "Unit")]
    public void FormatPullRequestsFormatsJiraPullRequests()
    {
        var pullRequests = new[]
        {
            TestData.CreateJiraPullRequestLink(id: 101, status: "OPEN", destinationBranch: "main"),
            TestData.CreateJiraPullRequestLink(id: 102, status: "MERGED", destinationBranch: "release/1.0"),
        };

        var formatted = QaQueuePresentationFormatting.FormatPullRequests(pullRequests);

        formatted.Should().Be("#101:OPEN->main, #102:MERGED->release/1.0");
        QaQueuePresentationFormatting.FormatPullRequests([]).Should().Be("-");
    }

    [Fact(DisplayName = "FormatMergedPullRequests formats merged pull requests")]
    [Trait("Category", "Unit")]
    public void FormatMergedPullRequestsFormatsMergedPullRequests()
    {
        var pullRequests = new[]
        {
            TestData.CreateMergedPullRequest(id: 201),
            TestData.CreateMergedPullRequest(id: 202),
        };

        var formatted = QaQueuePresentationFormatting.FormatMergedPullRequests(pullRequests);

        formatted.Should().Be("#201, #202");
        QaQueuePresentationFormatting.FormatMergedPullRequests([]).Should().Be("-");
    }

    [Fact(DisplayName = "FormatBranchNames removes blanks and duplicates")]
    [Trait("Category", "Unit")]
    public void FormatBranchNamesRemovesBlanksAndDuplicates()
    {
        var formatted = QaQueuePresentationFormatting.FormatBranchNames(
            [
                new BranchName("feature/qa-1"),
                new BranchName("feature/qa-1"),
                new BranchName("release/1.0"),
            ]);

        formatted.Should().Be("feature/qa-1, release/1.0");
        QaQueuePresentationFormatting.FormatBranchNames([]).Should().Be("-");
    }

    [Fact(DisplayName = "FormatAlertText returns duplicate marker when needed")]
    [Trait("Category", "Unit")]
    public void FormatAlertTextReturnsDuplicateMarkerWhenNeeded()
    {
        QaQueuePresentationFormatting.FormatAlertText(true).Should().Be("MULTI-ENTRY");
        QaQueuePresentationFormatting.FormatAlertText(false).Should().Be("-");
    }

    [Fact(DisplayName = "BuildIssueUrl builds absolute Jira browse URLs")]
    [Trait("Category", "Unit")]
    public void BuildIssueUrlBuildsAbsoluteJiraBrowseUrls()
    {
        var formatted = QaQueuePresentationFormatting.BuildIssueUrl(
            new Uri("https://jira.example.test/browse/", UriKind.Absolute),
            new JiraIssueKey("QA-123"));

        formatted.Should().Be("https://jira.example.test/browse/QA-123");
    }

    [Theory(DisplayName = "FormatDuration formats milliseconds seconds and minutes")]
    [Trait("Category", "Unit")]
    [InlineData(500, "500ms")]
    [InlineData(1500, "1.500s")]
    [InlineData(61000, "1m 1.000s")]
    public void FormatDurationFormatsMillisecondsSecondsAndMinutes(int milliseconds, string expected)
    {
        var formatted = QaQueuePresentationFormatting.FormatDuration(TimeSpan.FromMilliseconds(milliseconds));

        formatted.Should().Be(expected);
    }

    [Theory(DisplayName = "FormatBytes formats bytes kilobytes and megabytes")]
    [Trait("Category", "Unit")]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(1572864, "1.5 MB")]
    public void FormatBytesFormatsBytesKilobytesAndMegabytes(long bytes, string expected)
    {
        var formatted = QaQueuePresentationFormatting.FormatBytes(bytes);

        formatted.Should().Be(expected);
    }
}
