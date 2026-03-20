using System.Globalization;

namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a validated pull request identifier.
/// </summary>
internal readonly record struct PullRequestId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestId"/> struct.
    /// </summary>
    /// <param name="value">The pull request identifier.</param>
    public PullRequestId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>
    /// Gets the raw pull request identifier.
    /// </summary>
    public int Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
