using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
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

        var (statusCode, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict."),
            InvalidStateException => (StatusCodes.Status400BadRequest, "Invalid request state."),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
            ExternalDependencyException => (StatusCodes.Status502BadGateway, "External dependency failure."),
            AuthenticationException => (StatusCodes.Status401Unauthorized, "Authentication required."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error.")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

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
                Detail = validationException.Message,
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(validationProblem);
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
