namespace QAQueueManager.Presentation.Shared;

/// <summary>
/// Represents an issue link and its visual emphasis settings.
/// </summary>
/// <param name="Key">The Jira issue key.</param>
/// <param name="Url">The Jira browse URL.</param>
/// <param name="Highlight">Whether the issue should be visually highlighted.</param>
internal sealed record QaQueuePresentationIssueRef(
    string Key,
    string Url,
    bool Highlight = false);
