namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized branch name.
/// </summary>
internal readonly record struct BranchName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BranchName"/> struct.
    /// </summary>
    /// <param name="value">The branch name value.</param>
    public BranchName(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized branch name.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the placeholder branch used when the source data does not provide one.
    /// </summary>
    public static BranchName Unknown { get; } = new("-");

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
