using QAQueueManager.Models.Domain;

namespace QAQueueManager.Abstractions;

/// <summary>
/// Renders the QA report to the console.
/// </summary>
internal interface IQaQueuePresentationService
{
    /// <summary>
    /// Writes the supplied report to the interactive console output.
    /// </summary>
    /// <param name="report">The report to render.</param>
    void Render(QaQueueReport report);
}
