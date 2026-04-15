using System.Security.Claims;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Infrastructure.Extensions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

const long maxDocumentUploadSizeBytes = 100 * 1024 * 1024;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxDocumentUploadSizeBytes;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("AutonomousResearchAgent"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddHttpClientInstrumentation()
            .AddSource("AutonomousJobRunner")
            .AddSource("DatabaseJobWorker");
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        var paperImportMeter = new Meter("AutonomousResearchAgent.PaperImport", "1.0.0");
        var summaryMeter = new Meter("AutonomousResearchAgent.Summary", "1.0.0");
        var searchMeter = new Meter("AutonomousResearchAgent.Search", "1.0.0");

        paperImportMeter.CreateCounter<long>("paper_imports_total", description: "Total number of paper import operations");
        paperImportMeter.CreateCounter<long>("papers_imported", description: "Total number of papers imported");
        paperImportMeter.CreateHistogram<double>("paper_import_duration_ms", description: "Paper import duration in milliseconds");
        paperImportMeter.CreateCounter<long>("paper_import_failures_total", description: "Total number of failed paper imports");

        summaryMeter.CreateCounter<long>("summaries_generated_total", description: "Total number of summaries generated");
        summaryMeter.CreateHistogram<double>("summary_generation_duration_ms", description: "Summary generation duration in milliseconds");
        summaryMeter.CreateCounter<long>("summary_generation_failures_total", description: "Total number of failed summary generations");

        searchMeter.CreateHistogram<double>("search_latency_ms", description: "Search latency in milliseconds");
        searchMeter.CreateCounter<long>("search_requests_total", description: "Total number of search requests");
        searchMeter.CreateCounter<long>("search_failures_total", description: "Total number of failed search requests");
    });

builder.Services.AddApiLayer(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthAndOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseCors();
}
app.UseHttpsRedirection();
app.UseDocumentUploadSizeLimit(maxDocumentUploadSizeBytes);
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "dev-user"),
                new Claim(ClaimTypes.Name, "Developer"),
                new Claim(ClaimTypes.Role, "ReadOnly"),
            ], "Development"));
        }
        await next();
    });
}
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapOpenApi("/openapi/{documentName}.json");
app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();

public partial class Program;
