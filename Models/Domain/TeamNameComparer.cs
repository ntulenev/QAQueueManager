namespace QAQueueManager.Models.Domain;

/// <summary>
/// Compares team names while keeping the fallback team section at the end.
/// </summary>
internal sealed class TeamNameComparer : IComparer<TeamName>
{
    /// <summary>
    /// Gets the singleton comparer instance.
    /// </summary>
    public static TeamNameComparer Instance { get; } = new();

    /// <summary>
    /// Compares two team names for report ordering.
    /// </summary>
    /// <param name="x">The left team name.</param>
    /// <param name="y">The right team name.</param>
    /// <returns>A comparison result suitable for sorting.</returns>
    public int Compare(TeamName x, TeamName y)
    {
        if (x == y)
        {
            return 0;
        }

        return x == TeamName.NoTeam
            ? 1
            : y == TeamName.NoTeam
            ? -1
            : string.Compare(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
    }
}
