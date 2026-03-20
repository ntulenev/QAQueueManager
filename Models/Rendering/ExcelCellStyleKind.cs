namespace QAQueueManager.Models.Rendering;

/// <summary>
/// Defines the workbook cell styles used by the Excel formatter.
/// </summary>
internal enum ExcelCellStyleKind
{
    /// <summary>
    /// The default cell style.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The title cell style.
    /// </summary>
    Title = 1,

    /// <summary>
    /// The metadata label cell style.
    /// </summary>
    MetadataLabel = 2,

    /// <summary>
    /// The section title cell style.
    /// </summary>
    SectionTitle = 3,

    /// <summary>
    /// The table header cell style.
    /// </summary>
    Header = 4,

    /// <summary>
    /// The table body cell style.
    /// </summary>
    Body = 5,

    /// <summary>
    /// The hyperlink cell style.
    /// </summary>
    Hyperlink = 6,

    /// <summary>
    /// The muted informational cell style.
    /// </summary>
    Muted = 7,

    /// <summary>
    /// The warning cell style.
    /// </summary>
    Warning = 8,
}
