using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NotesApp.Api.Data;
using NotesApp.Api.Models;
using NotesApp.Api.Services;

namespace NotesApp.Api.Tests;

public static class TestJwt
{
    public const string Key = "chave-de-teste-com-mais-de-32-caracteres-0123456789";
    public const string Issuer = "NoteesApp.Api";
    public const string Audience = "NoteesApp.Client";
}

public static class HttpClientAuthExtensions
{
    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}

// API real (pipeline, validação, JWT, middleware) com o INoteService mockado.
// A connection string aponta para uma porta fechada: serve para provar o 503 do readiness.
public class MockedServiceApiFactory : WebApplicationFactory<Program>
{
    public Mock<INoteService> NoteService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", TestJwt.Key);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Host=127.0.0.1;Port=1;Database=x;Username=u;Password=p;Timeout=2");

        builder.ConfigureServices(services =>
        {
            services.AddScoped(_ => NoteService.Object);
        });
    }

    /// <summary>Cliente já autenticado como o usuário informado (access token real, assinado com a chave de teste).</summary>
    public HttpClient CreateClientFor(Guid userId)
    {
        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokens.CreateAccessToken(new User { Id = userId, Email = $"{userId:N}@teste.com" });

        return CreateClient().WithBearer(token.Token);
    }
}

// API real com banco em memória: permite exercitar registro, login, refresh e isolamento de notas ponta a ponta.
public class InMemoryApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected virtual string AuthPermitLimit => "1000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", TestJwt.Key);
        builder.UseSetting("RateLimiting:AuthPermitLimit", AuthPermitLimit);
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");

        builder.ConfigureServices(services =>
        {
            // Troca o Npgsql pelo provider em memória (dois providers no mesmo container não convivem)
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}

public class RateLimitedInMemoryApiFactory : InMemoryApiFactory
{
    protected override string AuthPermitLimit => "3";
}
