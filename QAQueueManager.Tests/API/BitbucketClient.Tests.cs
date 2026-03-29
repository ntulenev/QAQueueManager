using System.Net;

using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.API;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.API;

public sealed class BitbucketClientTests
{
    [Fact(DisplayName = "GetPullRequestAsync maps Bitbucket responses and caches loaded pull requests")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestAsyncMapsBitbucketResponsesAndCachesLoadedPullRequests()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            var requestUri = request.RequestUri!.ToString();
            requestUri.Should().Contain("/repositories/workspace/repo-a/pullrequests/42");
            requestUri.Should().Contain("fields=id%2Cstate%2Cupdated_on%2Cmerge_commit.hash%2Csource.branch.name%2Cdestination.branch.name%2Cdestination.repository.full_name%2Cdestination.repository.name%2Clinks.html.href");
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new BitbucketPullRequestResponse
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
                    Branch = new BitbucketBranchDto { Name = "feature/qa-42" },
                    Repository = new BitbucketRepositoryDto { FullName = "workspace/repo-a", Name = "Repo A" }
                },
                Links = new BitbucketPullRequestLinksDto
                {
                    Html = new BitbucketHrefDto { Href = "https://bitbucket.example.test/workspace/repo-a/pull-requests/42" }
                }
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var first = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(42), cts.Token);
        var second = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(42), cts.Token);

        // Assert
        first.Should().NotBeNull();
        first!.State.Should().Be(PullRequestState.Merged);
        first.RepositoryFullName.Should().Be(new RepositoryFullName("workspace/repo-a"));
        first.RepositoryDisplayName.Should().Be(new RepositoryDisplayName("Repo A"));
        first.SourceBranch.Should().Be(new BranchName("feature/qa-42"));
        first.DestinationBranch.Should().Be(new BranchName("main"));
        first.MergeCommitHash.Should().Be(new CommitHash("abcdef1"));
        second.Should().BeSameAs(first);
        handler.SendCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GetTagsByCommitHashAsync filters tags by commit hash, sorts them, and caches repository tags")]
    [Trait("Category", "Unit")]
    public async Task GetTagsByCommitHashAsyncFiltersTagsSortsThemAndCachesRepositoryTags()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            var requestUri = request.RequestUri!.ToString();
            requestUri.Should().Contain("/repositories/workspace/repo-a/refs/tags");
            requestUri.Should().Contain("q=target.hash");
            requestUri.Should().Contain("fields=values.name%2Cvalues.date%2Cvalues.target.hash%2Cnext");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new BitbucketTagPageResponse
            {
                Values =
                [
                    new BitbucketTagDto { Name = "1.2.0", Target = new BitbucketTagTargetDto { Hash = "abcdef1" } },
                    new BitbucketTagDto { Name = "1.10.0", Target = new BitbucketTagTargetDto { Hash = "abcdef1" } },
                    new BitbucketTagDto { Name = "1.2.0", Target = new BitbucketTagTargetDto { Hash = "abcdef1" } },
                    new BitbucketTagDto { Name = "ignored", Target = new BitbucketTagTargetDto { Hash = "1234567" } }
                ]
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var first = await client.GetTagsByCommitHashAsync(new RepositorySlug("repo-a"), new CommitHash("abcdef1"), cts.Token);
        var second = await client.GetTagsByCommitHashAsync(new RepositorySlug("repo-a"), new CommitHash("abcdef1"), cts.Token);

        // Assert
        first.Select(static tag => tag.Name.Value).Should().ContainInOrder("1.10.0", "1.2.0");
        second.Should().BeSameAs(first);
        handler.SendCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GetTagsByCommitHashAsync follows opaque next links for filtered tag pages")]
    [Trait("Category", "Unit")]
    public async Task GetTagsByCommitHashAsyncWhenFilteredTagResponseIsPagedFollowsNextLinks()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            var requestUri = request.RequestUri!.ToString();
            if (requestUri.Contains("cursor=2", StringComparison.Ordinal))
            {
                return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new BitbucketTagPageResponse
                {
                    Values =
                    [
                        new BitbucketTagDto { Name = "1.0.1", Target = new BitbucketTagTargetDto { Hash = "abcdef1" } }
                    ]
                }));
            }

            requestUri.Should().Contain("q=target.hash");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new BitbucketTagPageResponse
            {
                Values =
                [
                    new BitbucketTagDto { Name = "1.0.0", Target = new BitbucketTagTargetDto { Hash = "abcdef1" } }
                ],
                Next = "https://api.bitbucket.org/2.0/repositories/workspace/repo-a/refs/tags?cursor=2"
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var tags = await client.GetTagsByCommitHashAsync(new RepositorySlug("repo-a"), new CommitHash("abcdef1"), cts.Token);

        // Assert
        tags.Select(static tag => tag.Name.Value).Should().ContainInOrder("1.0.1", "1.0.0");
        handler.SendCalls.Should().Be(2);
    }

    [Fact(DisplayName = "GetPullRequestAsync caches missing pull requests after Bitbucket transport failures")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestAsyncWhenTransportFailsReturnsNullAndCachesMissingPullRequest()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{}""", HttpStatusCode.NotFound)));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var first = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(404), cts.Token);
        var second = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(404), cts.Token);

        // Assert
        first.Should().BeNull();
        second.Should().BeNull();
        handler.SendCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GetPullRequestAsync retries transport when Bitbucket returns a null payload")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestAsyncWhenBitbucketReturnsNullPayloadDoesNotCacheResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """null""")));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var first = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(405), cts.Token);
        var second = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(405), cts.Token);

        // Assert
        first.Should().BeNull();
        second.Should().BeNull();
        handler.SendCalls.Should().Be(2);
    }

    [Fact(DisplayName = "GetPullRequestAsync falls back to normalized defaults when Bitbucket omits repository metadata")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestAsyncWhenRepositoryMetadataIsMissingUsesFallbackValues()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new BitbucketPullRequestResponse
            {
                Id = 406,
                State = " ",
                MergeCommit = new BitbucketCommitRefDto { Hash = "not-a-hash" },
                Destination = new BitbucketPullRequestSideDto
                {
                    Branch = new BitbucketBranchDto { Name = " " },
                    Repository = new BitbucketRepositoryDto()
                },
                Source = new BitbucketPullRequestSideDto
                {
                    Branch = new BitbucketBranchDto { Name = " " },
                    Repository = new BitbucketRepositoryDto()
                },
                Links = new BitbucketPullRequestLinksDto
                {
                    Html = new BitbucketHrefDto { Href = " " }
                }
            })));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var pullRequest = await client.GetPullRequestAsync(new RepositorySlug("repo-a"), new PullRequestId(406), cts.Token);

        // Assert
        pullRequest.Should().NotBeNull();
        pullRequest!.State.Should().Be(PullRequestState.Unknown);
        pullRequest.RepositoryFullName.Should().Be(new RepositoryFullName("workspace/repo-a"));
        pullRequest.RepositoryDisplayName.Should().Be(new RepositoryDisplayName("repo-a"));
        pullRequest.SourceBranch.Should().Be(BranchName.Unknown);
        pullRequest.DestinationBranch.Should().Be(BranchName.Unknown);
        pullRequest.HtmlUrl.Should().BeNull();
        pullRequest.MergeCommitHash.Should().BeNull();
    }

    [Fact(DisplayName = "GetTagsByCommitHashAsync caches empty repository tag sets after transport failures")]
    [Trait("Category", "Unit")]
    public async Task GetTagsByCommitHashAsyncWhenRepositoryTagLookupFailsCachesEmptyRepositoryResults()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(/*lang=json,strict*/ """{}""", HttpStatusCode.Forbidden)));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bitbucket.example.test/", UriKind.Absolute)
        };
        var transport = new BitbucketTransport(httpClient, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));
        var client = new BitbucketClient(transport, Options.Create(new BitbucketOptions
        {
            Workspace = "workspace",
            RetryCount = 0
        }));

        // Act
        var first = await client.GetTagsByCommitHashAsync(new RepositorySlug("repo-a"), new CommitHash("abcdef1"), cts.Token);
        var second = await client.GetTagsByCommitHashAsync(new RepositorySlug("repo-a"), new CommitHash("abcdef2"), cts.Token);

        // Assert
        first.Should().BeEmpty();
        second.Should().BeEmpty();
        handler.SendCalls.Should().Be(1);
    }
}
