using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Models.Rendering;
using QAQueueManager.Presentation.Excel;

namespace QAQueueManager.Tests.Presentation.Excel;

public sealed class QaQueueExcelMarkupKeyTests
{
    [Fact(DisplayName = "CreateNoCode builds no-code markup key")]
    [Trait("Category", "Unit")]
    public void CreateNoCodeBuildsNoCodeMarkupKey()
    {
        var markupKey = QaQueueExcelMarkupKey.CreateNoCode(
            new ExcelSheetName("Core"),
            new JiraIssueKey("QA-1"));

        markupKey.Value.Should().Be("Core|__no-code__|QA-1");
    }

    [Fact(DisplayName = "CreateWithoutMerge builds repository issue markup key")]
    [Trait("Category", "Unit")]
    public void CreateWithoutMergeBuildsRepositoryIssueMarkupKey()
    {
        var markupKey = QaQueueExcelMarkupKey.CreateWithoutMerge(
            new ExcelSheetName("Core"),
            new RepositoryFullName("workspace/repo-a"),
            new JiraIssueKey("QA-2"));

        markupKey.Value.Should().Be("Core|workspace/repo-a|QA-2");
    }

    [Fact(DisplayName = "CreateMerged builds merged issue markup key")]
    [Trait("Category", "Unit")]
    public void CreateMergedBuildsMergedIssueMarkupKey()
    {
        var markupKey = QaQueueExcelMarkupKey.CreateMerged(
            new ExcelSheetName("Core"),
            new RepositoryFullName("workspace/repo-a"),
            new JiraIssueKey("QA-2"),
            new ArtifactVersion("1.2.3"));

        markupKey.Value.Should().Be("Core|workspace/repo-a|QA-2|1.2.3");
    }
}
