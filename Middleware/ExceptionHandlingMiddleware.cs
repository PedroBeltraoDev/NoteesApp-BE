using System.Net;
using NotesApp.Api.DTOs;

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
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "Nota não encontrada"),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Erro interno do servidor")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsJsonAsync(ApiResponseDto<object>.Fail(message));
    }
}
