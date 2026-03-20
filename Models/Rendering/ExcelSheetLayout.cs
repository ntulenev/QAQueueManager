using System.Collections.ObjectModel;

namespace QAQueueManager.Models.Rendering;

internal sealed class ExcelSheetLayout(string name)
{
    public string Name { get; set; } = name;

    public Dictionary<int, double> ColumnWidths { get; } = [];

    public Collection<ExcelTableRange> TableRanges { get; } = [];

    public Dictionary<string, ExcelCellStyleKind> CellStyles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Hyperlinks { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record ExcelTableRange(
    int HeaderRow,
    int StartColumnIndex,
    int EndColumnIndex,
    int DataStartRow,
    int DataEndRow);

internal enum ExcelCellStyleKind
{
    Default = 0,
    Title = 1,
    MetadataLabel = 2,
    SectionTitle = 3,
    Header = 4,
    Body = 5,
    Hyperlink = 6,
    Muted = 7,
    Warning = 8,
}
