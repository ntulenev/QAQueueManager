namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized Jira workflow status.
/// </summary>
internal readonly record struct JiraIssueStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueStatus"/> struct.
    /// </summary>
    /// <param name="value">The Jira status value.</param>
    public JiraIssueStatus(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized status value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the fallback status used when the source data does not provide one.
    /// </summary>
    public static JiraIssueStatus Unknown { get; } = new("-");

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
