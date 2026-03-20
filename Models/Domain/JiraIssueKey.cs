namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized Jira issue key.
/// </summary>
internal readonly record struct JiraIssueKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueKey"/> struct.
    /// </summary>
    /// <param name="value">The Jira issue key.</param>
    public JiraIssueKey(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized issue key value.
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
