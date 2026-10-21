using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NotesApp.Api.Models;
using Xunit;

namespace NotesApp.Api.Tests;

// Sobe a API de verdade (pipeline, validação, JWT, middleware) com o INoteService substituído por um mock.
public class ApiIntegrationTests : IClassFixture<MockedServiceApiFactory>
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly MockedServiceApiFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(MockedServiceApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientFor(UserId);
        _factory.NoteService.Reset();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---------- Listagem / paginação

    [Fact]
    public async Task GetNotes_WithoutPaginationParameters_ReturnsEverythingWithoutPagination()
    {
        _factory.NoteService.Setup(s => s.GetFilteredAsync(UserId, null, null, null))
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
        _factory.NoteService.Setup(s => s.GetPagedAsync(UserId, null, "Trabalho", "urgente", 2, 5))
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
        _factory.NoteService.Setup(s => s.GetPagedAsync(UserId, null, null, null, 1, 10))
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
    public async Task UnknownNote_Returns404InApiFormat()
    {
        _factory.NoteService.Setup(s => s.GetByIdAsync(UserId, 99)).ThrowsAsync(new KeyNotFoundException());

        var response = await _client.GetAsync("/api/notes/99");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    // ---------- Autenticação nas rotas de notas

    [Theory]
    [InlineData("GET", "/api/notes")]
    [InlineData("GET", "/api/notes/1")]
    [InlineData("POST", "/api/notes")]
    [InlineData("PUT", "/api/notes/1")]
    [InlineData("DELETE", "/api/notes/1")]
    [InlineData("GET", "/api/notes/folders")]
    [InlineData("GET", "/api/notes/tags")]
    [InlineData("GET", "/api/auth/me")]
    public async Task ProtectedRoutes_WithoutToken_Return401InApiFormat(string method, string url)
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
        _factory.NoteService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProtectedRoute_WithGarbageToken_Returns401()
    {
        var client = _factory.CreateClient().WithBearer("isto-nao-e-um-jwt");

        var response = await client.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithTokenSignedByAnotherKey_Returns401()
    {
        var token = CreateToken(key: "outra-chave-completamente-diferente-0123456789ab");

        var response = await _factory.CreateClient().WithBearer(token).GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithExpiredToken_Returns401AndSaysItExpired()
    {
        var token = CreateToken(lifetime: TimeSpan.FromMinutes(-10));

        var response = await _factory.CreateClient().WithBearer(token).GetAsync("/api/notes");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Token expirado", body.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("issuer-errado", TestJwt.Audience)]
    [InlineData(TestJwt.Issuer, "audience-errada")]
    public async Task ProtectedRoute_WithWrongIssuerOrAudience_Returns401(string issuer, string audience)
    {
        var token = CreateToken(issuer: issuer, audience: audience);

        var response = await _factory.CreateClient().WithBearer(token).GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithTokenWithoutSubject_Returns401()
    {
        var token = CreateToken(subject: null);
        _factory.NoteService.Setup(s => s.GetFilteredAsync(It.IsAny<Guid>(), null, null, null))
            .ReturnsAsync(new List<Note>());

        var response = await _factory.CreateClient().WithBearer(token).GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Handler_ReceivesTheUserIdFromTheTokenSubject()
    {
        var someoneElse = Guid.NewGuid();
        _factory.NoteService.Setup(s => s.GetFilteredAsync(someoneElse, null, null, null))
            .ReturnsAsync(new List<Note>());

        var response = await _factory.CreateClientFor(someoneElse).GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.NoteService.Verify(s => s.GetFilteredAsync(someoneElse, null, null, null), Times.Once);
    }

    // ---------- Health

    [Fact]
    public async Task Health_Liveness_IsPublicAndDoesNotTouchTheDatabase()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_Ready_WhenDatabaseIsUnreachable_Returns503()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health/ready");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("unhealthy", body.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
    }

    // ---------- Util

    private static string CreateToken(
        string key = TestJwt.Key,
        string issuer = TestJwt.Issuer,
        string audience = TestJwt.Audience,
        TimeSpan? lifetime = null,
        string? subject = "")
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime ?? TimeSpan.FromMinutes(5));
        var claims = new List<Claim>();

        if (subject is not null)
            claims.Add(new Claim("sub", subject == "" ? Guid.NewGuid().ToString() : subject));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = audience,
            // NotBefore sempre antes de Expires, mesmo quando o token já nasce vencido
            NotBefore = (expires < now ? expires : now).AddMinutes(-20),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });
    }
}
