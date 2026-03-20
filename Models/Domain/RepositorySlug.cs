namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a validated Bitbucket repository slug.
/// </summary>
internal readonly record struct RepositorySlug
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositorySlug"/> struct.
    /// </summary>
    /// <param name="value">The repository slug value.</param>
    public RepositorySlug(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the canonical slug value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the fallback slug used when the repository cannot be resolved.
    /// </summary>
    public static RepositorySlug Unknown { get; } = new("unknown");

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Contains('/') || normalized.Contains('\\'))
        {
            throw new ArgumentException("Repository slug must not contain path separators.", nameof(value));
        }

        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Repository slug must not contain whitespace.", nameof(value));
        }

        return normalized;
    }
}
