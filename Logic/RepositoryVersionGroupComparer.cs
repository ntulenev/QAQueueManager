using QAQueueManager.API;

namespace QAQueueManager.Logic;

/// <summary>
/// Compares artifact versions for repository output ordering.
/// </summary>
internal sealed class RepositoryVersionGroupComparer : IComparer<string>
{
    /// <summary>
    /// Gets the singleton comparer instance.
    /// </summary>
    public static RepositoryVersionGroupComparer Instance { get; } = new();

    /// <summary>
    /// Compares two version strings in report order.
    /// </summary>
    /// <param name="x">The left version value.</param>
    /// <param name="y">The right version value.</param>
    /// <returns>A comparison result suitable for sorting.</returns>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (string.Equals(x, "Version not found", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(y, "Version not found", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        var compare = VersionNameComparer.Instance.Compare(x, y);
        return compare == 0 ? 0 : -compare;
    }
}
