namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized pull request state.
/// </summary>
internal readonly record struct PullRequestState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestState"/> struct.
    /// </summary>
    /// <param name="value">The pull request state value.</param>
    public PullRequestState(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized state value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the fallback state used when the source data does not provide one.
    /// </summary>
    public static PullRequestState Unknown { get; } = new("UNKNOWN");

    /// <summary>
    /// Gets the merged state token.
    /// </summary>
    public static PullRequestState Merged { get; } = new("MERGED");

    /// <summary>
    /// Gets a value indicating whether the state represents a merged pull request.
    /// </summary>
    public bool IsMerged => string.Equals(Value, Merged.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
