using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NotesApp.Api.Models;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

// Fluxo completo (registro, login, refresh, logout e isolamento de notas) sobre um banco em memória.
// Os índices únicos não são aplicados pelo provider em memória; isso é coberto pelas checagens do serviço.
public class AuthFlowTests : IClassFixture<InMemoryApiFactory>
{
    private const string Password = "senha-segura-123";

    private readonly InMemoryApiFactory _factory;

    public AuthFlowTests(InMemoryApiFactory factory)
    {
        _factory = factory;
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@teste.com";

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
        => (await ReadJsonAsync(response)).GetProperty("data");

    private async Task<JsonElement> RegisterAsync(string email, string password = Password)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync(response);
    }

    private HttpClient ClientFor(JsonElement auth)
        => _factory.CreateClient().WithBearer(auth.GetProperty("accessToken").GetString()!);

    private static object NewNote(string title = "Minha nota", string folder = "Trabalho", string[]? tags = null) => new
    {
        title,
        content = "Conteúdo da nota com mais de 10 caracteres",
        folder,
        tags = tags ?? ["importante"],
        isPinned = false
    };

    // ---------- Registro

    [Fact]
    public async Task Register_ReturnsTokensAndNormalizesEmail()
    {
        var email = NewEmail();

        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new { email = $"  {email.ToUpperInvariant()}  ", password = Password });
        var data = await DataAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(email, data.GetProperty("user").GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("refreshToken").GetString()));
        Assert.True(data.GetProperty("refreshTokenExpiresAt").GetDateTime() > data.GetProperty("accessTokenExpiresAt").GetDateTime());
    }

    [Fact]
    public async Task Register_NeverReturnsThePasswordHash()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new { email = NewEmail(), password = Password });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, raw);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409_EvenWithDifferentCase()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("nao-e-email", "senha-segura-123")]
    [InlineData("", "senha-segura-123")]
    [InlineData("a@teste.com", "curta")]
    [InlineData("a@teste.com", "")]
    public async Task Register_WithInvalidData_Returns400WithFieldErrors(string email, string password)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password });
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task Register_WithOversizedPassword_Returns400()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new { email = NewEmail(), password = new string('a', 129) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Login

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var data = await DataAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, data.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_Returns401WithTheSameMessage()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var wrongPassword = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email, password = "senha-errada-123" });
        var unknownEmail = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await ReadJsonAsync(wrongPassword)).GetProperty("message").GetString(),
            (await ReadJsonAsync(unknownEmail)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Me_ReturnsTheAuthenticatedUser()
    {
        var email = NewEmail();
        var auth = await RegisterAsync(email);

        var response = await ClientFor(auth).GetAsync("/api/auth/me");
        var data = await DataAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.Equal(auth.GetProperty("user").GetProperty("id").GetString(), data.GetProperty("id").GetString());
    }

    // ---------- Refresh / logout

    [Fact]
    public async Task Refresh_RotatesTheToken_AndTheOldOneStopsWorking()
    {
        var auth = await RegisterAsync(NewEmail());
        var oldRefresh = auth.GetProperty("refreshToken").GetString();
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = oldRefresh });
        var firstData = await DataAsync(first);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(oldRefresh, firstData.GetProperty("refreshToken").GetString());

        // O novo access token funciona
        var me = await ClientFor(firstData).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // Reusar o token antigo é recusado...
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = oldRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // ...e derruba também o token novo (suspeita de roubo)
        var afterReuse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = firstData.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "token-que-nao-existe" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutBody_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken_AndIsIdempotent()
    {
        var auth = await RegisterAsync(NewEmail());
        var refresh = auth.GetProperty("refreshToken").GetString();
        var client = _factory.CreateClient();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = refresh });
        var again = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = refresh });
        var unknown = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "desconhecido" });
        var refreshAfter = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfter.StatusCode);
    }

    [Fact]
    public async Task RefreshTokensOfOtherSessions_SurviveALogout()
    {
        var email = NewEmail();
        var first = await RegisterAsync(email);
        var secondResponse = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var second = await DataAsync(secondResponse);

        await _factory.CreateClient().PostAsJsonAsync("/api/auth/logout", new { refreshToken = first.GetProperty("refreshToken").GetString() });
        var stillWorks = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second.GetProperty("refreshToken").GetString() });

        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
    }

    // ---------- Isolamento das notas entre usuários

    [Fact]
    public async Task Notes_AreIsolatedBetweenUsers()
    {
        var alice = ClientFor(await RegisterAsync(NewEmail()));
        var bob = ClientFor(await RegisterAsync(NewEmail()));

        var created = await alice.PostAsJsonAsync("/api/notes", NewNote("Nota da Alice", "Segredos", ["privada"]));
        var noteId = (await DataAsync(created)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Bob não vê a nota de nenhuma forma
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/notes/{noteId}")).StatusCode);
        Assert.Equal(0, (await DataAsync(await bob.GetAsync("/api/notes"))).GetArrayLength());
        Assert.Equal(0, (await DataAsync(await bob.GetAsync("/api/notes?folder=Segredos"))).GetArrayLength());
        Assert.Equal(0, (await DataAsync(await bob.GetAsync("/api/notes?page=1&pageSize=10"))).GetArrayLength());
        Assert.Equal(0, (await DataAsync(await bob.GetAsync("/api/notes/folders"))).GetArrayLength());
        // Filtro por tag e lista de tags usam unnest do Postgres, que o provider em memória não traduz:
        // o escopo por usuário dessas duas consultas é coberto em NoteRepositoryQueryTests (SQL gerado).

        // Bob não consegue alterar nem apagar
        var update = await bob.PutAsJsonAsync($"/api/notes/{noteId}", NewNote("Invasão do Bob"));
        var delete = await bob.DeleteAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // A nota da Alice segue intacta
        var original = await DataAsync(await alice.GetAsync($"/api/notes/{noteId}"));
        Assert.Equal("Nota da Alice", original.GetProperty("title").GetString());
        Assert.Equal(1, (await DataAsync(await alice.GetAsync("/api/notes"))).GetArrayLength());
        Assert.Equal("Segredos", (await DataAsync(await alice.GetAsync("/api/notes/folders")))[0].GetString());
    }

    [Fact]
    public async Task Notes_CanBeCreatedUpdatedAndDeletedByTheirOwner()
    {
        var user = ClientFor(await RegisterAsync(NewEmail()));

        var created = await user.PostAsJsonAsync("/api/notes", NewNote("Primeira versão"));
        var noteId = (await DataAsync(created)).GetProperty("id").GetInt32();

        var updated = await user.PutAsJsonAsync($"/api/notes/{noteId}", NewNote("Segunda versão"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Segunda versão", (await DataAsync(updated)).GetProperty("title").GetString());

        Assert.Equal(HttpStatusCode.OK, (await user.DeleteAsync($"/api/notes/{noteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/notes/{noteId}")).StatusCode);
    }

    [Fact]
    public async Task Notes_IgnoreAnOwnerSentInTheBody()
    {
        var alice = await RegisterAsync(NewEmail());
        var bob = ClientFor(await RegisterAsync(NewEmail()));

        var payload = new
        {
            title = "Tentativa de forjar dono",
            content = "Conteúdo da nota com mais de 10 caracteres",
            userId = alice.GetProperty("user").GetProperty("id").GetString()
        };
        var created = await bob.PostAsJsonAsync("/api/notes", payload);
        var noteId = (await DataAsync(created)).GetProperty("id").GetInt32();

        // A nota pertence a quem estava autenticado (Bob), não ao id enviado no corpo
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/api/notes/{noteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ClientFor(alice).GetAsync($"/api/notes/{noteId}")).StatusCode);
    }

    [Fact]
    public async Task ValidTokenOfAnUnregisteredUser_SeesNoNotes_AndMeReturns401()
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var ghost = tokens.CreateAccessToken(new User { Email = "fantasma@teste.com" });
        var client = _factory.CreateClient().WithBearer(ghost.Token);

        Assert.Equal(0, (await DataAsync(await client.GetAsync("/api/notes"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}

public class AuthRateLimitTests : IClassFixture<RateLimitedInMemoryApiFactory>
{
    private readonly RateLimitedInMemoryApiFactory _factory;

    public AuthRateLimitTests(RateLimitedInMemoryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthEndpoints_AreRateLimited_AndAnswerInApiFormat()
    {
        var client = _factory.CreateClient();
        var attempt = new { email = "ninguem@teste.com", password = "senha-qualquer-123" };

        // Limite configurado para 3 por minuto nesta fábrica
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", attempt)).StatusCode);

        var blocked = await client.PostAsJsonAsync("/api/auth/login", attempt);
        var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task NonAuthEndpoints_AreNotAffectedByTheAuthLimit()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }
}
