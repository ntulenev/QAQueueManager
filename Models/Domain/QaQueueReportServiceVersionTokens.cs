namespace QAQueueManager.Models.Domain;

/// <summary>
/// Holds shared constant values used by the report service.
/// </summary>
internal static class QaQueueReportServiceVersionTokens
{
    /// <summary>
    /// Represents the fallback artifact version value when no matching tag is found.
    /// </summary>
    public const string VERSION_NOT_FOUND = "Version not found";

    /// <summary>
    /// Represents the fallback team name when an issue has no team mapping.
    /// </summary>
    public const string NO_TEAM = "No team";
}
