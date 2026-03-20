using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.API;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.API;

public sealed class JiraDevelopmentClientTests
{
    [Fact(DisplayName = "GetPullRequestsAsync maps and sorts Jira pull request links")]
    [Trait("Category", "Unit")]
    public async Task GetPullRequestsAsyncMapsAndSortsJiraPullRequestLinks()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            request.RequestUri!.ToString().Should().Contain("dataType=pullrequest");
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraDevelopmentDetailsResponse
            {
                Detail =
                [
                    new JiraDevelopmentDetailDto
                    {
                        PullRequests =
                        [
                            new JiraPullRequestDto
                            {
                                Id = "42",
                                Name = "PR 42",
                                Status = "MERGED",
                                Url = "https://bitbucket.example.test/workspace/repo-a/pull-requests/42",
                                RepositoryName = "workspace/repo-a",
                                RepositoryUrl = "https://bitbucket.example.test/workspace/repo-a",
                                Source = new JiraPullRequestBranchDto { Branch = "feature/qa-42" },
                                Destination = new JiraPullRequestBranchDto { Branch = "main" },
                                LastUpdate = "2026-03-20T09:00:00+00:00"
                            },
                            new JiraPullRequestDto
                            {
                                Id = "41",
                                Name = "PR 41",
                                Status = "OPEN",
                                Url = "https://bitbucket.example.test/workspace/repo-a/pull-requests/41",
                                RepositoryName = "workspace/repo-a",
                                RepositoryUrl = "https://bitbucket.example.test/workspace/repo-a",
                                Source = new JiraPullRequestBranchDto { Branch = "feature/qa-41" },
                                Destination = new JiraPullRequestBranchDto { Branch = "main" },
                                LastUpdate = "2026-03-20T08:00:00+00:00"
                            },
                            new JiraPullRequestDto
                            {
                                Id = "invalid",
                                RepositoryName = "workspace/repo-a"
                            }
                        ]
                    }
                ]
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions
        {
            RetryCount = 0,
            BitbucketApplicationType = "bitbucket",
            PullRequestDataType = "pullrequest"
        }));
        var client = new JiraDevelopmentClient(transport, Options.Create(new JiraOptions
        {
            RetryCount = 0,
            BitbucketApplicationType = "bitbucket",
            PullRequestDataType = "pullrequest",
            BranchDataType = "branch"
        }));

        // Act
        var pullRequests = await client.GetPullRequestsAsync(new JiraIssueId(101), cts.Token);

        // Assert
        pullRequests.Should().HaveCount(2);
        pullRequests.Select(static pr => pr.Id.Value).Should().ContainInOrder(42, 41);
        pullRequests[0].Status.Should().Be(PullRequestState.Merged);
        pullRequests[1].SourceBranch.Should().Be(new BranchName("feature/qa-41"));
    }

    [Fact(DisplayName = "GetBranchesAsync maps and sorts Jira branch links")]
    [Trait("Category", "Unit")]
    public async Task GetBranchesAsyncMapsAndSortsJiraBranchLinks()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            request.RequestUri!.ToString().Should().Contain("dataType=branch");
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new JiraDevelopmentDetailsResponse
            {
                Detail =
                [
                    new JiraDevelopmentDetailDto
                    {
                        Branches =
                        [
                            new JiraBranchDto
                            {
                                Name = "feature/qa-2",
                                Repository = new JiraRepositoryDto
                                {
                                    Name = "workspace/repo-b",
                                    Url = "https://bitbucket.example.test/workspace/repo-b"
                                }
                            },
                            new JiraBranchDto
                            {
                                Name = "feature/qa-1",
                                Repository = new JiraRepositoryDto
                                {
                                    Name = "workspace/repo-a",
                                    Url = "https://bitbucket.example.test/workspace/repo-a"
                                }
                            },
                            new JiraBranchDto
                            {
                                Name = "",
                                Repository = new JiraRepositoryDto { Name = "workspace/repo-c" }
                            }
                        ]
                    }
                ]
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var transport = new JiraTransport(httpClient, Options.Create(new JiraOptions
        {
            RetryCount = 0,
            BitbucketApplicationType = "bitbucket",
            BranchDataType = "branch"
        }));
        var client = new JiraDevelopmentClient(transport, Options.Create(new JiraOptions
        {
            RetryCount = 0,
            BitbucketApplicationType = "bitbucket",
            PullRequestDataType = "pullrequest",
            BranchDataType = "branch"
        }));

        // Act
        var branches = await client.GetBranchesAsync(new JiraIssueId(101), cts.Token);

        // Assert
        branches.Should().HaveCount(2);
        branches.Select(static branch => branch.RepositoryFullName.Value).Should().ContainInOrder("workspace/repo-a", "workspace/repo-b");
        branches[0].Name.Should().Be(new BranchName("feature/qa-1"));
    }
}
