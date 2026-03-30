using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using QAQueueManager.Abstractions;
using QAQueueManager.API;
using QAQueueManager.Logic;
using QAQueueManager.Models.Configuration;
using QAQueueManager.Models.Domain;
using QAQueueManager.Presentation;
using QAQueueManager.Presentation.Shared;
using QAQueueManager.Presentation.Excel;
using QAQueueManager.Presentation.Pdf;
using QAQueueManager.Transport;

using QuestPDF.Infrastructure;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
QuestPDF.Settings.License = LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<JiraOptions>()
    .Bind(builder.Configuration.GetSection("Jira"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<BitbucketOptions>()
    .Bind(builder.Configuration.GetSection("Bitbucket"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ReportOptions>()
    .Bind(builder.Configuration.GetSection("Report"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IHttpRequestTelemetryCollector, HttpRequestTelemetryCollector>();

builder.Services.AddHttpClient<JiraTransport>((sp, http) =>
{
    var settings = sp.GetRequiredService<IOptions<JiraOptions>>().Value;
    http.BaseAddress = new Uri(settings.BaseUrl.ToString().TrimEnd('/') + "/", UriKind.Absolute);

    var raw = $"{settings.Email}:{settings.ApiToken}";
    var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(static () => CreateHttpMessageHandler());

builder.Services.AddHttpClient<BitbucketTransport>((sp, http) =>
{
    var settings = sp.GetRequiredService<IOptions<BitbucketOptions>>().Value;
    http.BaseAddress = new Uri(settings.BaseUrl.ToString().TrimEnd('/') + "/", UriKind.Absolute);

    var raw = $"{settings.AuthEmail}:{settings.AuthApiToken}";
    var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(static () => CreateHttpMessageHandler());

builder.Services.AddTransient<IJiraIssueSearchClient, JiraIssueSearchClient>();
builder.Services.AddTransient<IJiraFieldResolver, JiraFieldResolver>();
builder.Services.AddTransient<IJiraSearchExecutor, JiraSearchExecutor>();
builder.Services.AddTransient<IJiraObjectMapper, JiraObjectMapper>();
builder.Services.AddTransient<IJiraIssueSearchMapper, JiraIssueSearchMapper>();
builder.Services.AddTransient<IJiraDevelopmentClient, JiraDevelopmentClient>();
builder.Services.AddTransient<IBitbucketClient, BitbucketClient>();
builder.Services.AddTransient<IArtifactVersionResolver, ArtifactVersionResolver>();
builder.Services.AddTransient<IRepositoryResolutionBuilder, RepositoryResolutionBuilder>();
builder.Services.AddTransient<IQaCodeIssueDetailsLoader, QaCodeIssueDetailsLoader>();
builder.Services.AddTransient<IQaQueueReportBuilder, QaQueueReportBuilder>();
builder.Services.AddTransient<QaQueueReportDocumentBuilder>();
builder.Services.AddTransient<IQaQueueReportService, QaQueueReportService>();
builder.Services.AddTransient<IQaQueueWorkflowRunner, QaQueueWorkflowRunner>();
builder.Services.AddTransient<IQaQueuePresentationService, SpectreQaQueuePresentationService>();
builder.Services.AddTransient<IQaQueueWorkflowProgressHost, SpectreQaQueueWorkflowProgressHost>();
builder.Services.AddTransient<IPdfReportRenderer, QuestPdfReportRenderer>();
builder.Services.AddTransient<IPdfReportFileStore, PdfReportFileStore>();
builder.Services.AddTransient<IPdfReportLauncher, PdfReportLauncher>();
builder.Services.AddTransient<IExcelWorkbookContentComposer, QaQueueExcelContentComposer>();
builder.Services.AddTransient<IWorkbookFormatter, OpenXmlWorkbookFormatter>();
builder.Services.AddTransient<IExcelReportRenderer, MiniExcelQaQueueReportRenderer>();
builder.Services.AddTransient<IExcelReportFileStore, ExcelReportFileStore>();
builder.Services.AddTransient<IQaQueueApplication, QaQueueApplication>();

using var host = builder.Build();

var app = host.Services.GetRequiredService<IQaQueueApplication>();
await app.RunAsync(CancellationToken.None).ConfigureAwait(false);

static HttpMessageHandler CreateHttpMessageHandler()
{
    return new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip
            | DecompressionMethods.Deflate
            | DecompressionMethods.Brotli
    };
}
