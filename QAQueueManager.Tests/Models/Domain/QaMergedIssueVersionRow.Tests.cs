using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class QaMergedIssueVersionRowTests
{
    [Fact(DisplayName = "QaMergedIssueVersionRow exposes multiple-version flag")]
    [Trait("Category", "Unit")]
    public void QaMergedIssueVersionRowExposesMultipleVersionFlag()
    {
        // Arrange
        var issue = TestData.CreateIssue();
        var mergedPullRequest = TestData.CreateMergedPullRequest(updatedOn: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero));

        // Act
        var mergedRow = new QaMergedIssueVersionRow(
            issue,
            new RepositoryFullName("workspace/repo-a"),
            new RepositorySlug("repo-a"),
            new ArtifactVersion("1.2.3"),
            [mergedPullRequest],
            HasMultipleVersions: true);

        // Assert
        mergedRow.HasMultipleVersions.Should().BeTrue();
    }
}
