using System.Net;
using System.Text.Json;

namespace NotesApp.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";
        
        response.Headers.Append("Access-Control-Allow-Origin", 
            context.Request.Headers["Origin"]);
        response.Headers.Append("Access-Control-Allow-Credentials", "true");
        
        var statusCode = exception switch
        {
            KeyNotFoundException => HttpStatusCode.NotFound,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            _ => HttpStatusCode.InternalServerError
        };
        
        response.StatusCode = (int)statusCode;
        
        var result = JsonSerializer.Serialize(new
        {
            error = exception.Message,
            statusCode = (int)statusCode
        });
        
        return response.WriteAsync(result);
    }
}