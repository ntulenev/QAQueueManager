using FluentAssertions;

using Microsoft.Extensions.Options;

using QAQueueManager.API;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Transport;
using QAQueueManager.Tests.Testing;

namespace QAQueueManager.Tests.API;

public sealed class JiraFieldResolverTests
{
    [Fact(DisplayName = "ResolveOptionalFieldsAsync trims aliases, resolves field definitions, and de-duplicates matches")]
    [Trait("Category", "Unit")]
    public async Task ResolveOptionalFieldsAsyncResolvesConfiguredAliasesAndDeduplicatesFields()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, cancellationToken) =>
        {
            request.RequestUri!.ToString().Should().Contain("rest/api/3/field");
            cancellationToken.CanBeCanceled.Should().BeTrue();
            cancellationToken.IsCancellationRequested.Should().BeFalse();
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new[]
            {
                new JiraFieldDefinitionResponse
                {
                    Id = "customfield_100",
                    Key = "customfield_100",
                    Name = "Team",
                    ClauseNames = ["Team", "cf[100]"]
                },
                new JiraFieldDefinitionResponse
                {
                    Id = "customfield_200",
                    Key = "customfield_200",
                    Name = "\" Squad \"",
                    ClauseNames = [" Squad "]
                }
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var resolver = new JiraFieldResolver(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var fields = await resolver.ResolveOptionalFieldsAsync("  \"Team\" ; Squad ; cf[100] ", cts.Token);

        // Assert
        fields.Should().Equal("customfield_100", "customfield_200");
        handler.SendCalls.Should().Be(1);
    }

    [Fact(DisplayName = "ResolveRequiredFieldAsync caches direct custom fields without loading Jira metadata")]
    [Trait("Category", "Unit")]
    public async Task ResolveRequiredFieldAsyncWithDirectCustomFieldSkipsMetadataLookup()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            throw new Xunit.Sdk.XunitException($"Unexpected request to {request.RequestUri}");
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var resolver = new JiraFieldResolver(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var field = await resolver.ResolveRequiredFieldAsync(" customfield_dev ", cts.Token);
        var optionalFields = await resolver.ResolveOptionalFieldsAsync("customfield_dev; customfield_dev", cts.Token);

        // Assert
        field.Should().Be("customfield_dev");
        optionalFields.Should().Equal("customfield_dev");
        handler.SendCalls.Should().Be(0);
    }

    [Fact(DisplayName = "ResolveRequiredFieldAsync throws when configured Jira field cannot be resolved")]
    [Trait("Category", "Unit")]
    public async Task ResolveRequiredFieldAsyncWhenAliasCannotBeResolvedThrowsInvalidOperationException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            request.RequestUri!.ToString().Should().Contain("rest/api/3/field");
            return Task.FromResult(RecordingHttpMessageHandler.CreateJsonResponse(new[]
            {
                new JiraFieldDefinitionResponse
                {
                    Id = "customfield_other",
                    Key = "customfield_other",
                    Name = "Other"
                }
            }));
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jira.example.test/", UriKind.Absolute)
        };
        var resolver = new JiraFieldResolver(new JiraTransport(httpClient, Options.Create(new JiraOptions { RetryCount = 0 })));

        // Act
        var act = async () => await resolver.ResolveRequiredFieldAsync("Development", cts.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development*");
    }
}
