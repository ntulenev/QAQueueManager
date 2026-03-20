using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;

namespace QAQueueManager.Abstractions;

internal interface IQaQueueApplication
{
    Task RunAsync(CancellationToken cancellationToken);
}

internal interface IQaQueueReportService
{
    Task<QaQueueReport> BuildAsync(
        IProgress<QaQueueBuildProgress>? progress,
        CancellationToken cancellationToken);
}

internal interface IJiraIssueSearchClient
{
    Task<IReadOnlyList<QaIssue>> SearchIssuesAsync(CancellationToken cancellationToken);
}

internal interface IJiraDevelopmentClient
{
    Task<IReadOnlyList<JiraPullRequestLink>> GetPullRequestsAsync(long issueId, CancellationToken cancellationToken);

    Task<IReadOnlyList<JiraBranchLink>> GetBranchesAsync(long issueId, CancellationToken cancellationToken);
}

internal interface IBitbucketClient
{
    Task<BitbucketPullRequest?> GetPullRequestAsync(
        string repositorySlug,
        int pullRequestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BitbucketTag>> GetTagsByCommitHashAsync(
        string repositorySlug,
        string commitHash,
        CancellationToken cancellationToken);
}

internal interface IQaQueuePresentationService
{
    void Render(QaQueueReport report);
}

internal interface IPdfReportRenderer
{
    byte[] Render(QaQueueReport report);
}

internal interface IPdfReportFileStore
{
    string Save(byte[] content, string suggestedPath);
}

internal interface IPdfReportLauncher
{
    void Launch(string path);
}

internal interface IExcelReportRenderer
{
    MemoryStream Render(QaQueueReport report);
}

internal interface IExcelReportFileStore
{
    string Save(Stream contentStream, string suggestedPath);
}

internal interface IExcelWorkbookContentComposer
{
    ExcelWorkbookData ComposeWorkbook(QaQueueReport report);
}

internal interface IWorkbookFormatter
{
    void Format(Stream workbookStream, IReadOnlyDictionary<string, ExcelSheetLayout> layouts);
}
