using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class RepositoryResolutionTests
{
    [Fact(DisplayName = "CreateUnknownWithoutMerge returns unknown repository with empty no-merge payload")]
    [Trait("Category", "Unit")]
    public void CreateUnknownWithoutMergeReturnsUnknownRepository()
    {
        // Act
        var resolution = RepositoryResolution.CreateUnknownWithoutMerge();

        // Assert
        resolution.Repository.Should().Be(RepositoryRef.Unknown);
        resolution.WithoutMerge.Should().NotBeNull();
        resolution.WithoutMerge!.PullRequests.Should().BeEmpty();
        resolution.WithoutMerge.BranchNames.Should().BeEmpty();
        resolution.Merged.Should().BeNull();
    }

    [Fact(DisplayName = "CreateMergedFallback builds artifact-not-found resolution from Jira candidate")]
    [Trait("Category", "Unit")]
    public void CreateMergedFallbackBuildsFallbackMergedResolution()
    {
        // Arrange
        var repositoryFullName = new RepositoryFullName("/");
        var repository = new RepositoryRef(repositoryFullName, RepositorySlug.Unknown);
        var candidate = TestData.CreateJiraPullRequestLink(
            id: 77,
            status: "MERGED",
            repositoryFullName: "/",
            destinationBranch: "main",
            lastUpdatedOn: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero));

        // Act
        var resolution = RepositoryResolution.CreateMergedFallback(repositoryFullName, repository, candidate);

        // Assert
        resolution.WithoutMerge.Should().BeNull();
        resolution.Merged.Should().NotBeNull();
        resolution.Merged!.Version.Should().Be(ArtifactVersion.NotFound);
        resolution.Merged.PullRequest.RepositoryFullName.Should().Be(repositoryFullName);
        resolution.Merged.PullRequest.RepositorySlug.Should().Be(RepositorySlug.Unknown);
        resolution.Merged.PullRequest.RepositoryDisplayName.Should().Be(new RepositoryDisplayName(RepositorySlug.Unknown.Value));
        resolution.Merged.PullRequest.Id.Should().Be(candidate.Id);
    }
}
