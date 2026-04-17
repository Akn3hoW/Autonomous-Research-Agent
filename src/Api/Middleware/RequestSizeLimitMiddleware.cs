using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AutonomousResearchAgent.Api.Middleware;

public sealed class RequestSizeLimitMiddleware(RequestDelegate next, long maxDocumentUploadSizeBytes)
{
    private static readonly PathString DocumentUploadPathPrefix = PathString.FromUriComponent("/api/v1/papers");

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ShouldApplySizeLimit(context.Request.Path))
        {
            var maxRequestBodyFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxRequestBodyFeature is not null)
            {
                maxRequestBodyFeature.MaxRequestBodySize = maxDocumentUploadSizeBytes;
            }
        }

        await next(context);
    }

    private static bool ShouldApplySizeLimit(PathString path)
    {
        if (!path.StartsWithSegments(DocumentUploadPathPrefix, out var remaining))
        {
            return false;
        }

        var pathValue = remaining.Value;
        if (string.IsNullOrEmpty(pathValue))
        {
            return false;
        }

        return pathValue.StartsWith("/documents", StringComparison.OrdinalIgnoreCase);
    }
}

public static class RequestSizeLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseDocumentUploadSizeLimit(this IApplicationBuilder builder, long maxDocumentUploadSizeBytes)
    {
        return builder.UseMiddleware<RequestSizeLimitMiddleware>(maxDocumentUploadSizeBytes);
    }
}
