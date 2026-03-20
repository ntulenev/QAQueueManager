using System.Collections.ObjectModel;

namespace QAQueueManager.Models.Rendering;

/// <summary>
/// Describes layout metadata for a single Excel worksheet.
/// </summary>
/// <param name="name">The worksheet name.</param>
internal sealed class ExcelSheetLayout(ExcelSheetName name)
{
    /// <summary>
    /// Gets or sets the worksheet name.
    /// </summary>
    public ExcelSheetName Name { get; set; } = name;

    /// <summary>
    /// Gets the configured column widths keyed by one-based column index.
    /// </summary>
    public Dictionary<int, double> ColumnWidths { get; } = [];

    /// <summary>
    /// Gets the table ranges that should receive table formatting.
    /// </summary>
    public Collection<ExcelTableRange> TableRanges { get; } = [];

    /// <summary>
    /// Gets the explicit cell styles keyed by cell reference.
    /// </summary>
    public Dictionary<string, ExcelCellStyleKind> CellStyles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the hyperlinks keyed by cell reference.
    /// </summary>
    public Dictionary<string, string> Hyperlinks { get; } = new(StringComparer.OrdinalIgnoreCase);
}
