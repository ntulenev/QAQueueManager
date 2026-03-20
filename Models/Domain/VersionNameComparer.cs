namespace QAQueueManager.Models.Domain;

/// <summary>
/// Compares version-like strings by numeric version parts.
/// </summary>
internal sealed class VersionNameComparer : IComparer<ArtifactVersion>
{
    /// <summary>
    /// Gets the singleton comparer instance.
    /// </summary>
    public static VersionNameComparer Instance { get; } = new();

    /// <summary>
    /// Compares two version-like strings.
    /// </summary>
    /// <param name="x">The left version value.</param>
    /// <param name="y">The right version value.</param>
    /// <returns>A comparison result suitable for sorting.</returns>
    public int Compare(ArtifactVersion x, ArtifactVersion y)
    {
        if (x == y)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(x.Value))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(y.Value))
        {
            return -1;
        }

        var xNumbers = ExtractNumbers(x.Value);
        var yNumbers = ExtractNumbers(y.Value);
        var max = Math.Max(xNumbers.Count, yNumbers.Count);

        for (var index = 0; index < max; index++)
        {
            var left = index < xNumbers.Count ? xNumbers[index] : 0;
            var right = index < yNumbers.Count ? yNumbers[index] : 0;
            var compare = right.CompareTo(left);
            if (compare != 0)
            {
                return compare;
            }
        }

        return string.Compare(y.Value, x.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static List<int> ExtractNumbers(string value)
    {
        var numbers = new List<int>();
        var current = 0;
        var inNumber = false;

        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                current = (current * 10) + (ch - '0');
                inNumber = true;
                continue;
            }

            if (inNumber)
            {
                numbers.Add(current);
                current = 0;
                inNumber = false;
            }
        }

        if (inNumber)
        {
            numbers.Add(current);
        }

        return numbers;
    }
}
