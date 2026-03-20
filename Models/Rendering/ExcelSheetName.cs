namespace QAQueueManager.Models.Rendering;

/// <summary>
/// Represents a validated Excel worksheet name.
/// </summary>
internal readonly record struct ExcelSheetName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelSheetName"/> struct.
    /// </summary>
    /// <param name="value">The worksheet name.</param>
    public ExcelSheetName(string value)
    {
        Value = Normalize(value);
    }

    /// <summary>
    /// Gets the worksheet name value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Attempts to create a validated sheet name.
    /// </summary>
    /// <param name="value">The candidate worksheet name.</param>
    /// <param name="sheetName">The created sheet name when validation succeeds.</param>
    /// <returns><see langword="true"/> when the worksheet name is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(string? value, out ExcelSheetName sheetName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            sheetName = default;
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length is 0 or > 31 || normalized.IndexOfAny(['\\', '/', '?', '*', '[', ']', ':']) >= 0)
        {
            sheetName = default;
            return false;
        }

        sheetName = new ExcelSheetName(normalized);
        return true;
    }

    /// <summary>
    /// Sanitizes a display label into a valid worksheet name.
    /// </summary>
    /// <param name="value">The source label.</param>
    /// <param name="fallback">The fallback worksheet name when the source is empty after sanitization.</param>
    /// <returns>A valid worksheet name.</returns>
    public static ExcelSheetName Sanitize(string? value, string fallback = "Sheet")
    {
        var filtered = new string((value ?? string.Empty)
            .Where(static ch => !"\\/?*[]:".Contains(ch))
            .ToArray())
            .Trim();

        if (filtered.Length == 0)
        {
            filtered = fallback.Trim();
        }

        if (filtered.Length > 31)
        {
            filtered = filtered[..31];
        }

        return new ExcelSheetName(filtered);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Length > 31)
        {
            throw new ArgumentException("Excel worksheet name must be 31 characters or fewer.", nameof(value));
        }

        if (normalized.IndexOfAny(['\\', '/', '?', '*', '[', ']', ':']) >= 0)
        {
            throw new ArgumentException("Excel worksheet name contains invalid characters.", nameof(value));
        }

        return normalized;
    }
}
