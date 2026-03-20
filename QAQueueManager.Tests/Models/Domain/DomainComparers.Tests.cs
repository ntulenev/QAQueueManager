using FluentAssertions;

using QAQueueManager.Models.Domain;

namespace QAQueueManager.Tests.Models.Domain;

public sealed class DomainComparersTests
{
    [Fact(DisplayName = "Version and team comparers order values as expected")]
    [Trait("Category", "Unit")]
    public void VersionAndTeamComparersOrderValuesAsExpected()
    {
        // Arrange
        var versions = new List<ArtifactVersion>
        {
            ArtifactVersion.NotFound,
            new("1.2.0"),
            new("1.10.0"),
            new("1.2.1")
        };
        var teams = new List<TeamName>
        {
            TeamName.NoTeam,
            new("Platform"),
            new("Core")
        };

        // Act
        versions.Sort(VersionNameComparer.Instance);
        teams.Sort(TeamNameComparer.Instance);

        // Assert
        versions.Should().ContainInOrder(
            new ArtifactVersion("1.10.0"),
            new ArtifactVersion("1.2.1"),
            new ArtifactVersion("1.2.0"),
            ArtifactVersion.NotFound);
        RepositoryVersionGroupComparer.Instance.Compare(new ArtifactVersion("1.10.0"), ArtifactVersion.NotFound).Should().BeLessThan(0);
        teams.Should().ContainInOrder(new TeamName("Core"), new TeamName("Platform"), TeamName.NoTeam);
    }

    [Fact(DisplayName = "Version, repository, and team comparers handle equality and fallback values")]
    [Trait("Category", "Unit")]
    public void VersionRepositoryAndTeamComparersHandleEqualityAndFallbackValues()
    {
        // Arrange
        var sameVersion = new ArtifactVersion("1.2.3");
        var lexicalLeft = new ArtifactVersion("v1.2");
        var lexicalRight = new ArtifactVersion("release-1-2");

        // Act
        var equalVersions = VersionNameComparer.Instance.Compare(sameVersion, sameVersion);
        var defaultLeft = VersionNameComparer.Instance.Compare(default, sameVersion);
        var defaultRight = VersionNameComparer.Instance.Compare(sameVersion, default);
        var lexicalTiebreak = VersionNameComparer.Instance.Compare(lexicalLeft, lexicalRight);
        var repositoryEquals = RepositoryVersionGroupComparer.Instance.Compare(sameVersion, sameVersion);
        var repositoryMissing = RepositoryVersionGroupComparer.Instance.Compare(ArtifactVersion.NotFound, sameVersion);
        var sameTeam = TeamNameComparer.Instance.Compare(new TeamName("Core"), new TeamName("Core"));
        var noTeamLeft = TeamNameComparer.Instance.Compare(TeamName.NoTeam, new TeamName("Core"));
        var noTeamRight = TeamNameComparer.Instance.Compare(new TeamName("Core"), TeamName.NoTeam);

        // Assert
        equalVersions.Should().Be(0);
        defaultLeft.Should().Be(1);
        defaultRight.Should().Be(-1);
        lexicalTiebreak.Should().Be(string.Compare(lexicalRight.Value, lexicalLeft.Value, StringComparison.OrdinalIgnoreCase));
        repositoryEquals.Should().Be(0);
        repositoryMissing.Should().BeGreaterThan(0);
        sameTeam.Should().Be(0);
        noTeamLeft.Should().BeGreaterThan(0);
        noTeamRight.Should().BeLessThan(0);
    }
}
