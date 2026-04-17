using System.Net;
using System.Threading.Tasks;
using AutonomousResearchAgent.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AutonomousResearchAgent.Api.Middleware;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AuditedAttribute : Attribute
{
}

public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Request.EnableBuffering();
        await _next(context);

        if (context.Request.Method is "POST" or "PATCH" or "PUT" or "DELETE")
        {
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<AuditedAttribute>() == null)
                return;

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                var auditService = context.RequestServices.GetRequiredService<IAuditService>();
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();

                var auditCommand = new AuditLogCommand(
                    UserId: string.IsNullOrEmpty(userId) ? null : Guid.TryParse(userId, out var uid) ? uid : null,
                    Action: context.Request.Method,
                    EntityType: GetEntityType(context.Request.Path),
                    EntityId: GetEntityId(context.Request.Path),
                    OldValues: null,
                    NewValues: await GetRequestBodyAsync(context),
                    Timestamp: DateTimeOffset.UtcNow,
                    IpAddress: ipAddress
                );

                try
                {
                    await auditService.LogAuditEventAsync(auditCommand, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write audit log for {UserId} {Action} {Path}",
                        auditCommand.UserId, auditCommand.Action, auditCommand.EntityType);
                }
            }
        }
    }

    private static string GetEntityType(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length > 2 ? segments[2] : "Unknown";
    }

    private static Guid? GetEntityId(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length > 3 && Guid.TryParse(segments[3], out var id))
            return id;
        return null;
    }

    private static readonly string[] SensitiveFields = ["password", "apiKey", "refreshToken", "secret", "token", "authorization"];

    private static async Task<string?> GetRequestBodyAsync(HttpContext context)
    {
        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : MaskSensitiveData(body);
    }

    private static string MaskSensitiveData(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var masked = MaskValue(doc.RootElement);
            return JsonSerializer.Serialize(masked);
        }
        catch
        {
            foreach (var field in SensitiveFields)
            {
                var pattern = $"\"{field}\"\\s*:\\s*\"[^\"]*\"";
                body = System.Text.RegularExpressions.Regex.Replace(body, pattern, $"\"{field}\":\"***\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return body;
        }
    }

    private static JsonElement MaskValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = SensitiveFields.Any(f => prop.Name.Equals(f, StringComparison.OrdinalIgnoreCase))
                    ? (object?)"***"
                    : MaskValue(prop.Value);
            }
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(dict));
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                list.Add(MaskValue(item));
            }
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(list));
        }
        return element;
    }
}