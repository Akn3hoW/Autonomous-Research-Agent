using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Infrastructure.Tests;

public sealed class JwtAuthMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_calls_next_when_no_exception()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_handles_not_found_exception()
    {
        var next = new RequestDelegate(_ => throw new NotFoundException("User", Guid.NewGuid()));
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_handles_conflict_exception()
    {
        var next = new RequestDelegate(_ => throw new ConflictException("Resource already exists."));
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_handles_invalid_state_exception()
    {
        var next = new RequestDelegate(_ => throw new InvalidStateException("Invalid operation."));
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_handles_validation_exception()
    {
        var validationException = new ValidationException(new[]
        {
            new FluentValidation.Results.ValidationFailure("Name", "Name is required."),
            new FluentValidation.Results.ValidationFailure("Email", "Email is invalid.")
        });
        var next = new RequestDelegate(_ => throw validationException);
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_handles_external_dependency_exception()
    {
        var next = new RequestDelegate(_ => throw new ExternalDependencyException("External service unavailable."));
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_handles_generic_exception_as_500()
    {
        var next = new RequestDelegate(_ => throw new InvalidOperationException("Something went wrong."));
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_does_not_expose_generic_exception_message_to_client()
    {
        var next = new RequestDelegate(_ => throw new InvalidOperationException("Sensitive error details"));
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_logs_error_on_exception()
    {
        var exception = new InvalidOperationException("Test error");
        var next = new RequestDelegate(_ => throw exception);
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_returns_problem_details_json()
    {
        var next = new RequestDelegate(_ => throw new NotFoundException("Test", Guid.NewGuid()));
        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Contains("Resource not found", body);
    }
}
