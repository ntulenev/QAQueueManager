namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized team name.
/// </summary>
internal readonly record struct TeamName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeamName"/> struct.
    /// </summary>
    /// <param name="value">The team name.</param>
    public TeamName(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized team name.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the fallback team token used when an issue has no team mapping.
    /// </summary>
    public static TeamName NoTeam { get; } = new(QaQueueReportServiceVersionTokens.NO_TEAM);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
