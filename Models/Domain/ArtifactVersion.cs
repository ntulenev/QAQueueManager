namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized artifact version token.
/// </summary>
internal readonly record struct ArtifactVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactVersion"/> struct.
    /// </summary>
    /// <param name="value">The artifact version value.</param>
    public ArtifactVersion(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized version value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the fallback version token used when version resolution fails.
    /// </summary>
    public static ArtifactVersion NotFound { get; } = new(QaQueueReportServiceVersionTokens.VERSION_NOT_FOUND);

    /// <summary>
    /// Gets a value indicating whether the version represents a missing version.
    /// </summary>
    public bool IsNotFound => string.Equals(Value, NotFound.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
