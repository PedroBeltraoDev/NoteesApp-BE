using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NotesApp.Api.Middleware;
using Xunit;

namespace NotesApp.Api.Tests;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> InvokeAsync(Exception exception)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, body.RootElement);
    }

    [Fact]
    public async Task KeyNotFoundException_Returns404()
    {
        var (status, body) = await InvokeAsync(new KeyNotFoundException("x"));

        Assert.Equal(404, status);
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task ArgumentException_Returns400WithMessage()
    {
        var (status, body) = await InvokeAsync(new ArgumentException("Título é obrigatório"));

        Assert.Equal(400, status);
        Assert.Equal("Título é obrigatório", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutLeakingDetails()
    {
        var (status, body) = await InvokeAsync(new InvalidOperationException("senha do banco: 123"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("senha", body.GetProperty("message").GetString());
    }
}
