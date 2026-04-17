using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private const string ErrorTypeBase = "https://api.autonomousresearch.ai/errors/";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);

        var isDevelopment = context.RequestServices?.GetService<IWebHostEnvironment>()?.IsDevelopment() ?? false;

        var (statusCode, title, errorType) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found.", "not-found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict.", "conflict"),
            InvalidStateException => (StatusCodes.Status400BadRequest, "Invalid request state.", "invalid-state"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed.", "validation-failed"),
            ExternalDependencyException => (StatusCodes.Status502BadGateway, "External dependency failure.", "external-dependency-failure"),
            AuthenticationException => (StatusCodes.Status401Unauthorized, "Authentication required.", "authentication-required"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error.", "unexpected-server-error")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(x => x.ErrorMessage).Distinct().ToArray());

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Title = title,
                Status = statusCode,
                Type = ErrorTypeBase + "validation-failed",
                Detail = isDevelopment ? validationException.Message : "Validation failed.",
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(validationProblem);
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            Type = ErrorTypeBase + errorType,
            Detail = "An unexpected error occurred.",
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString();

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
