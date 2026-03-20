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

    /// <summary>
    /// Creates a repository slug from a repository full name.
    /// </summary>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <returns>The resolved repository slug, or <see cref="Unknown"/> when it cannot be derived.</returns>
    public static RepositorySlug FromRepositoryFullName(string? repositoryFullName)
    {
        if (string.IsNullOrWhiteSpace(repositoryFullName))
        {
            return Unknown;
        }

        var normalized = repositoryFullName.Trim().Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? Unknown : new RepositorySlug(parts[^1]);
    }

    /// <summary>
    /// Creates a repository slug from a repository full name.
    /// </summary>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <returns>The resolved repository slug, or <see cref="Unknown"/> when it cannot be derived.</returns>
    public static RepositorySlug FromRepositoryFullName(RepositoryFullName repositoryFullName) =>
        FromRepositoryFullName(repositoryFullName.Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        return normalized.Contains('/', StringComparison.Ordinal) || normalized.Contains('\\', StringComparison.Ordinal)
            ? throw new ArgumentException("Repository slug must not contain path separators.", nameof(value))
            : normalized.Any(char.IsWhiteSpace)
            ? throw new ArgumentException("Repository slug must not contain whitespace.", nameof(value))
            : normalized;
    }
}
