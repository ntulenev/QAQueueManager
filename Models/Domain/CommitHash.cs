namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a validated Git commit hash.
/// </summary>
internal readonly record struct CommitHash
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommitHash"/> struct.
    /// </summary>
    /// <param name="value">The commit hash value.</param>
    public CommitHash(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the canonical commit hash value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Attempts to create a validated commit hash.
    /// </summary>
    /// <param name="value">The candidate hash value.</param>
    /// <param name="hash">The created hash when validation succeeds.</param>
    /// <returns><see langword="true"/> when the hash is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(string? value, out CommitHash hash)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            hash = default;
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length is < 7 or > 40 || normalized.Any(static ch => !Uri.IsHexDigit(ch)))
        {
            hash = default;
            return false;
        }

        hash = new CommitHash(normalized);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Length is < 7 or > 40)
        {
            throw new ArgumentException("Commit hash must be between 7 and 40 hex characters.", nameof(value));
        }

        if (normalized.Any(static ch => !Uri.IsHexDigit(ch)))
        {
            throw new ArgumentException("Commit hash must contain only hexadecimal characters.", nameof(value));
        }

        return normalized;
    }
}
