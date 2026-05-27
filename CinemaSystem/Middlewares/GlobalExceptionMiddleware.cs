using System.Net;
using System.Text.Json;
using CinemaSystem.Contracts.Common;

namespace CinemaSystem.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception while processing request.");
            await WriteErrorResponseAsync(context, exception);
        }
    }

    private static Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errorCode) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized.", "UNAUTHORIZED"),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message, "BAD_REQUEST"),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message, "BAD_REQUEST"),
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message, "NOT_FOUND"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", "INTERNAL_SERVER_ERROR")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Fail(message, errorCode);
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
