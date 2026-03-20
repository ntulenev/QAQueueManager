using System.Reflection;

using FluentAssertions;

using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation.Pdf;

namespace QAQueueManager.Tests.Presentation.Pdf;

public sealed class PdfReportLauncherTests
{
    [Fact(DisplayName = "PdfReportLauncher exposes Launch contract accepting report paths")]
    [Trait("Category", "Unit")]
    public void PdfReportLauncherExposesLaunchContractAcceptingReportPaths()
    {
        // Arrange
        var method = typeof(PdfReportLauncher).GetMethod(nameof(PdfReportLauncher.Launch), BindingFlags.Instance | BindingFlags.Public);

        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(void));
        method.GetParameters().Select(static parameter => parameter.ParameterType).Should().ContainSingle().Which.Should().Be<ReportFilePath>();
    }

    [Fact(DisplayName = "PdfReportLauncher executes the shell launch path for report files")]
    [Trait("Category", "Unit")]
    public void PdfReportLauncherExecutesShellLaunchPathForReportFiles()
    {
        // Arrange
        var launcher = new PdfReportLauncher();
        var missingPath = new ReportFilePath(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf"));

        // Act
        Action act = () => launcher.Launch(missingPath);

        // Assert
        act.Should().Throw<Exception>()
            .Where(static ex => ex.Message.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase));
    }
}
