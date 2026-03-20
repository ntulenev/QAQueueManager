namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized repository full name.
/// </summary>
internal readonly record struct RepositoryFullName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryFullName"/> struct.
    /// </summary>
    /// <param name="value">The repository full name.</param>
    public RepositoryFullName(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized repository full name.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the fallback repository name used when the source data does not provide one.
    /// </summary>
    public static RepositoryFullName Unknown { get; } = new("Unknown repository");

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().Replace('\\', '/');
    }
}
