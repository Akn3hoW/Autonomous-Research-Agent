using System.Security.Claims;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AutonomousResearchAgent.Api.Hubs;
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
            if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
            {
                policy.SetIsOriginAllowed(_ => true);
            }
            else
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<AuditMiddleware>();
app.UseCors();
app.UseHsts();
app.UseHttpsRedirection();
app.UseDocumentUploadSizeLimit(maxDocumentUploadSizeBytes);
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseRateLimiter();
if (app.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("ENABLE_DEV_AUTH") == "true")
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
app.MapHub<JobStatusHub>("/hubs/jobs");
app.MapHealthChecks("/health");
app.MapOpenApi("/openapi/{documentName}.json");
app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();

public partial class Program;
