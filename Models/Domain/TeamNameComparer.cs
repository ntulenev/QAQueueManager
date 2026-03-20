namespace QAQueueManager.Models.Domain;

/// <summary>
/// Compares team names while keeping the fallback team section at the end.
/// </summary>
internal sealed class TeamNameComparer : IComparer<string>
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
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        return string.Equals(x, QaQueueReportServiceVersionTokens.NO_TEAM, StringComparison.OrdinalIgnoreCase)
            ? 1
            : string.Equals(y, QaQueueReportServiceVersionTokens.NO_TEAM, StringComparison.OrdinalIgnoreCase)
            ? -1
            : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
