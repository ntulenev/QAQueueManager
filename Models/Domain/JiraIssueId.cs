using System.Globalization;

namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a validated Jira issue identifier.
/// </summary>
internal readonly record struct JiraIssueId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueId"/> struct.
    /// </summary>
    /// <param name="value">The Jira issue identifier.</param>
    public JiraIssueId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>
    /// Gets the raw Jira issue identifier.
    /// </summary>
    public long Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
