namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a validated report file path.
/// </summary>
internal readonly record struct ReportFilePath
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportFilePath"/> struct.
    /// </summary>
    /// <param name="value">The report file path.</param>
    public ReportFilePath(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the normalized path value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(Path.GetFileName(normalized))
            ? throw new ArgumentException("Report file path must include a file name.", nameof(value))
            : normalized;
    }
}
