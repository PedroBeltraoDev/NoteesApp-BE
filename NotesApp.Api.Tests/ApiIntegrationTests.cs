using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NotesApp.Api.Models;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

// Sobe a API de verdade (pipeline, validação, middleware) com o INoteService substituído por um mock.
// A connection string aponta para uma porta fechada: serve para provar o 503 do readiness.
public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.TestApiFactory>
{
    public class TestApiFactory : WebApplicationFactory<Program>
    {
        public Mock<INoteService> NoteService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=x;Username=u;Password=p;Timeout=2");

            builder.ConfigureServices(services =>
            {
                services.AddScoped(_ => NoteService.Object);
            });
        }
    }

    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.NoteService.Reset();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task GetNotes_WithoutPaginationParameters_ReturnsEverythingWithoutPagination()
    {
        _factory.NoteService.Setup(s => s.GetFilteredAsync(null, null))
            .ReturnsAsync(new List<Note> { new() { Id = 1, Title = "A" }, new() { Id = 2, Title = "B" } });

        var response = await _client.GetAsync("/api/notes");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.GetProperty("data").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("pagination").ValueKind);
    }

    [Fact]
    public async Task GetNotes_WithPageAndPageSize_ReturnsPageAndPaginationMetadata()
    {
        _factory.NoteService.Setup(s => s.GetPagedAsync("Trabalho", "urgente", 2, 5))
            .ReturnsAsync(new PagedResult<Note>([new Note { Id = 6, Title = "F" }], 11));

        var response = await _client.GetAsync("/api/notes?folder=Trabalho&tag=urgente&page=2&pageSize=5");
        var body = await ReadJsonAsync(response);
        var pagination = body.GetProperty("pagination");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("data").GetArrayLength());
        Assert.Equal(2, pagination.GetProperty("page").GetInt32());
        Assert.Equal(5, pagination.GetProperty("pageSize").GetInt32());
        Assert.Equal(11, pagination.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, pagination.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task GetNotes_WithOnlyPageSize_DefaultsToFirstPage()
    {
        _factory.NoteService.Setup(s => s.GetPagedAsync(null, null, 1, 10))
            .ReturnsAsync(new PagedResult<Note>([], 0));

        var response = await _client.GetAsync("/api/notes?pageSize=10");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("pagination").GetProperty("page").GetInt32());
        Assert.Equal(0, body.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-3")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task GetNotes_WithInvalidPagination_Returns400(string query)
    {
        var response = await _client.GetAsync($"/api/notes?{query}");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Health_Liveness_DoesNotTouchTheDatabase()
    {
        var response = await _client.GetAsync("/api/health");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_Ready_WhenDatabaseIsUnreachable_Returns503()
    {
        var response = await _client.GetAsync("/api/health/ready");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("unhealthy", body.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
    }

    [Fact]
    public async Task UnknownNote_Returns404InApiFormat()
    {
        _factory.NoteService.Setup(s => s.GetByIdAsync(99)).ThrowsAsync(new KeyNotFoundException());

        var response = await _client.GetAsync("/api/notes/99");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
    }
}
