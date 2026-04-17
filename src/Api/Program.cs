using System.Security.Claims;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AutonomousResearchAgent.Api.Hubs;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Api.Services;
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
            .AddRuntimeInstrumentation()
            .AddMeter("AutonomousResearchAgent.Business");
    });

builder.Services.AddApiLayer(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthAndOpenApi();
builder.Services.AddSignalR();
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
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
            if (allowedOrigins.Length == 0)
            {
                throw new InvalidOperationException("AllowedOrigins configuration is missing. In production, you must explicitly specify allowed origins.");
            }
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

var meter = new System.Diagnostics.Metrics.Meter("AutonomousResearchAgent.Business");
var jobsCompletedCounter = meter.CreateCounter<long>("jobs.completed", description: "Number of jobs completed successfully");
var jobsFailedCounter = meter.CreateCounter<long>("jobs.failed", description: "Number of jobs failed");
var cacheHitCounter = meter.CreateCounter<long>("cache.hits", description: "Number of cache hits");
var cacheMissCounter = meter.CreateCounter<long>("cache.misses", description: "Number of cache misses");
var jobDurationHistogram = meter.CreateHistogram<double>("jobs.duration_ms", description: "Job duration in milliseconds");

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<AuditMiddleware>();
app.UseCors();
app.UseHsts();
app.UseHttpsRedirection();
app.UseDocumentUploadSizeLimit(maxDocumentUploadSizeBytes);
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<JobStatusHub>("/hubs/jobs");
app.MapHealthChecks("/health");
app.MapOpenApi("/openapi/{documentName}.json");
app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();

public partial class Program;
