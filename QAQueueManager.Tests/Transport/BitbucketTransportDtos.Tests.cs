using FluentAssertions;

using QAQueueManager.Transport;

namespace QAQueueManager.Tests.Transport;

public sealed class BitbucketTransportDtosTests
{
    [Fact(DisplayName = "Bitbucket transport DTOs expose assigned properties")]
    [Trait("Category", "Unit")]
    public void BitbucketTransportDtosExposeAssignedProperties()
    {
        // Arrange
        var dto = new BitbucketPullRequestResponse
        {
            Id = 42,
            State = "MERGED",
            UpdatedOn = new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.Zero),
            MergeCommit = new BitbucketCommitRefDto { Hash = "abcdef1" },
            Destination = new BitbucketPullRequestSideDto
            {
                Branch = new BitbucketBranchDto { Name = "main" },
                Repository = new BitbucketRepositoryDto { FullName = "workspace/repo-a", Name = "Repo A" }
            },
            Source = new BitbucketPullRequestSideDto
            {
                Branch = new BitbucketBranchDto { Name = "feature/qa-1" },
                Repository = new BitbucketRepositoryDto { FullName = "workspace/repo-a", Name = "Repo A" }
            },
            Links = new BitbucketPullRequestLinksDto
            {
                Html = new BitbucketHrefDto { Href = "https://bitbucket.example.test/workspace/repo-a/pull-requests/42" }
            }
        };
        var tagPage = new BitbucketTagPageResponse
        {
            Values =
            [
                new BitbucketTagDto
                {
                    Name = "1.2.3",
                    Date = dto.UpdatedOn,
                    Target = new BitbucketTagTargetDto { Hash = "abcdef1" }
                }
            ],
            Next = "https://bitbucket.example.test/next"
        };

        // Assert
        dto.Id.Should().Be(42);
        dto.MergeCommit!.Hash.Should().Be("abcdef1");
        dto.Destination!.Branch!.Name.Should().Be("main");
        dto.Source!.Repository!.Name.Should().Be("Repo A");
        dto.Links!.Html!.Href.Should().Contain("/pull-requests/42");
        tagPage.Values.Should().ContainSingle();
        tagPage.Values[0].Target!.Hash.Should().Be("abcdef1");
        tagPage.Next.Should().Be("https://bitbucket.example.test/next");
    }
}
