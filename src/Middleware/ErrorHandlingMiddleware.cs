using System.Net;
using System.Text.Json;
using NexoSferaApi.Models.Responses;

namespace NexoSferaApi.Middleware;

/// <summary>
/// Global error handling middleware
/// Catches all unhandled exceptions and returns consistent error responses
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            ArgumentNullException ex => (
                HttpStatusCode.BadRequest,
                CreateErrorResponse("Invalid argument", ex.Message, ex)
            ),
            ArgumentException ex => (
                HttpStatusCode.BadRequest,
                CreateErrorResponse("Invalid argument", ex.Message, ex)
            ),
            KeyNotFoundException ex => (
                HttpStatusCode.NotFound,
                CreateErrorResponse("Resource not found", ex.Message, ex)
            ),
            UnauthorizedAccessException ex => (
                HttpStatusCode.Unauthorized,
                CreateErrorResponse("Unauthorized", ex.Message, ex)
            ),
            InvalidOperationException ex when ex.Message.Contains("Sfera") => (
                HttpStatusCode.ServiceUnavailable,
                CreateErrorResponse("Sfera connection error", "Unable to connect to Nexo Sfera. Please check the connection settings.", ex)
            ),
            InvalidOperationException ex => (
                HttpStatusCode.BadRequest,
                CreateErrorResponse("Invalid operation", ex.Message, ex)
            ),
            TimeoutException ex => (
                HttpStatusCode.GatewayTimeout,
                CreateErrorResponse("Operation timeout", "The operation timed out. Please try again.", ex)
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                CreateErrorResponse("Internal server error", GetUserFriendlyMessage(exception), exception)
            )
        };

        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private ApiResponse<object> CreateErrorResponse(string message, string detail, Exception ex)
    {
        var errors = new List<string> { detail };

        // In development, include more details
        if (_environment.IsDevelopment())
        {
            if (ex.InnerException != null)
            {
                errors.Add($"Inner: {ex.InnerException.Message}");
            }
            errors.Add($"Type: {ex.GetType().Name}");
        }

        return ApiResponse<object>.Error(message, errors);
    }

    private string GetUserFriendlyMessage(Exception ex)
    {
        // Map common SDK exceptions to user-friendly messages
        var message = ex.Message;

        if (message.Contains("Blad") || message.Contains("Error"))
        {
            // SDK validation errors
            return "A validation error occurred while processing the request.";
        }

        if (message.Contains("Connection") || message.Contains("SQL"))
        {
            return "A database connection error occurred. Please try again later.";
        }

        if (_environment.IsDevelopment())
        {
            return message;
        }

        return "An unexpected error occurred. Please contact support if the problem persists.";
    }
}

/// <summary>
/// Extension methods for error handling middleware
/// </summary>
public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
