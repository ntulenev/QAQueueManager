namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized repository display name.
/// </summary>
internal readonly record struct RepositoryDisplayName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryDisplayName"/> struct.
    /// </summary>
    /// <param name="value">The repository display name.</param>
    public RepositoryDisplayName(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized repository display name.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
