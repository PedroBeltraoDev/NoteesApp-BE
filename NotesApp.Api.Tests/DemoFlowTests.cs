using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotesApp.Api.Data;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

public class DemoLimitedApiFactory : InMemoryApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Demo:MaxActive", "2");
    }
}

public class OneMinuteDemoApiFactory : InMemoryApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Demo:Minutes", "1");
    }
}

// Conta demo ponta a ponta (pipeline real, JWT, middleware) sobre um banco em memória
public class DemoFlowTests : IClassFixture<InMemoryApiFactory>
{
    private readonly InMemoryApiFactory _factory;

    public DemoFlowTests(InMemoryApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

    private static async Task<JsonElement> StartDemoAsync(InMemoryApiFactory factory)
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/demo", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await DataAsync(response);
    }

    private static HttpClient ClientFor(InMemoryApiFactory factory, JsonElement auth)
        => factory.CreateClient().WithBearer(auth.GetProperty("accessToken").GetString()!);

    [Fact]
    public async Task Demo_CreatesAnAccountWithNoBodyNoEmailAndNoPassword()
    {
        var before = DateTime.UtcNow;

        var auth = await StartDemoAsync(_factory);

        var user = auth.GetProperty("user");
        Assert.True(user.GetProperty("isDemo").GetBoolean());
        Assert.EndsWith("@" + AuthService.DemoEmailDomain, user.GetProperty("email").GetString());
        var ends = user.GetProperty("demoExpiresAt").GetDateTime();
        Assert.InRange(ends, before.AddMinutes(29.9), DateTime.UtcNow.AddMinutes(30.1));
        Assert.False(string.IsNullOrEmpty(auth.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(auth.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Demo_WorksLikeANormalAccount_AndComesWithExampleContent()
    {
        var demo = ClientFor(_factory, await StartDemoAsync(_factory));

        var notes = await DataAsync(await demo.GetAsync("/api/notes"));
        var folders = await DataAsync(await demo.GetAsync("/api/folders"));
        var me = await DataAsync(await demo.GetAsync("/api/auth/me"));

        Assert.Equal(3, notes.GetArrayLength());
        Assert.Equal("Exemplos", folders[0].GetProperty("name").GetString());
        Assert.Equal(2, folders[0].GetProperty("noteCount").GetInt32());
        Assert.True(me.GetProperty("isDemo").GetBoolean());

        // E cria, edita e exclui como qualquer conta
        var created = await demo.PostAsJsonAsync("/api/notes", new { title = "Minha nota na demo", content = "Texto" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await DataAsync(created)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await demo.PutAsJsonAsync($"/api/notes/{id}", new { title = "Editada", content = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await demo.DeleteAsync($"/api/notes/{id}")).StatusCode);
    }

    [Fact]
    public async Task EachDemoIsIsolatedFromTheOthers()
    {
        var first = ClientFor(_factory, await StartDemoAsync(_factory));
        var second = ClientFor(_factory, await StartDemoAsync(_factory));
        await first.PostAsJsonAsync("/api/notes", new { title = "Só da primeira", content = "" });

        var firstNotes = await DataAsync(await first.GetAsync("/api/notes"));
        var secondNotes = await DataAsync(await second.GetAsync("/api/notes"));

        Assert.Equal(4, firstNotes.GetArrayLength());
        Assert.Equal(3, secondNotes.GetArrayLength());
    }

    [Fact]
    public async Task ADemoHasNoPassword_LoginWithItsEmailIsAlwaysRefused()
    {
        var email = (await StartDemoAsync(_factory)).GetProperty("user").GetProperty("email").GetString();

        foreach (var password in new[] { "!", "senha-qualquer-123", "12345678" })
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task NobodyCanRegisterAnEmailOfTheDemoDomain()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new { email = $"pessoa@{AuthService.DemoEmailDomain}", password = "senha-segura-123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheRefreshTokenRenewsTheSessionDuringTheDemo_KeepingTheSameDeadline()
    {
        var auth = await StartDemoAsync(_factory);
        var deadline = auth.GetProperty("user").GetProperty("demoExpiresAt").GetDateTime();

        var refreshed = await _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var user = (await DataAsync(refreshed)).GetProperty("user");
        Assert.True(user.GetProperty("isDemo").GetBoolean());
        Assert.Equal(deadline, user.GetProperty("demoExpiresAt").GetDateTime()); // renovar a sessão não alonga a demo
    }

    // ---------- Expirar

    private async Task ExpireAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        db.Entry(user).Property(u => u.DemoExpiresAt).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    private async Task<(int Users, int Notes, int Folders)> CountsForAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
        var id = user?.Id ?? Guid.Empty;
        return (user is null ? 0 : 1, await db.Notes.CountAsync(n => n.UserId == id), await db.Folders.CountAsync(f => f.UserId == id));
    }

    [Fact]
    public async Task AfterTheDemoEnds_RefreshIsRefusedWithTheDemoExpiredCode_AndEverythingIsDeleted()
    {
        var auth = await StartDemoAsync(_factory);
        var email = auth.GetProperty("user").GetProperty("email").GetString()!;
        Assert.Equal((1, 3, 1), await CountsForAsync(email));
        await ExpireAsync(email);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("demo_expired", body.GetProperty("errors").GetProperty("code")[0].GetString());
        Assert.Equal("O tempo da conta demo acabou", body.GetProperty("message").GetString());
        Assert.Equal((0, 0, 0), await CountsForAsync(email)); // conta, notas e pastas foram embora
    }

    [Fact]
    public async Task CreatingANewDemoCleansUpTheExpiredOnesNobodyCameBackFor()
    {
        var abandoned = (await StartDemoAsync(_factory)).GetProperty("user").GetProperty("email").GetString()!;
        var active = (await StartDemoAsync(_factory)).GetProperty("user").GetProperty("email").GetString()!;
        await ExpireAsync(abandoned);

        await StartDemoAsync(_factory);

        Assert.Equal((0, 0, 0), await CountsForAsync(abandoned));
        Assert.Equal((1, 3, 1), await CountsForAsync(active)); // a que ainda está valendo não é tocada
    }

    [Fact]
    public async Task ARegularAccountIsNeverTouchedByTheCleanup()
    {
        var email = $"real-{Guid.NewGuid():N}@teste.com";
        var registered = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = "senha-segura-123" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var real = ClientFor(_factory, await DataAsync(registered));
        await real.PostAsJsonAsync("/api/notes", new { title = "Minha nota de verdade", content = "" });

        var demo = await StartDemoAsync(_factory);
        await ExpireAsync(demo.GetProperty("user").GetProperty("email").GetString()!);
        await StartDemoAsync(_factory); // dispara a limpeza

        Assert.Equal(1, (await DataAsync(await real.GetAsync("/api/notes"))).GetArrayLength());
        Assert.Equal((1, 1, 0), await CountsForAsync(email));
    }

    [Fact]
    public async Task TheAccessTokenCannotOutliveTheDemo()
    {
        var factory = new OneMinuteDemoApiFactory();
        var auth = await StartDemoAsync(factory);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.GetProperty("accessToken").GetString());

        // O normal seria 15 min; numa demo de 1 minuto o token vale 1 minuto, no máximo
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddSeconds(61));
        Assert.Equal(jwt.ValidTo, auth.GetProperty("accessTokenExpiresAt").GetDateTime(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WithTooManyActiveDemosANewOneIsRefusedWith503()
    {
        var factory = new DemoLimitedApiFactory();
        await StartDemoAsync(factory);
        await StartDemoAsync(factory);

        var response = await factory.CreateClient().PostAsync("/api/auth/demo", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("contas demo ativas", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task TheDemoEndpointIsRateLimitedLikeTheOtherAuthRoutes()
    {
        using var factory = new RateLimitedInMemoryApiFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await client.PostAsync("/api/auth/demo", null)).StatusCode);

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    // ---------- Excluir a conta

    private async Task<(HttpClient Client, JsonElement Auth, string Email)> RegisterAsync(InMemoryApiFactory factory, string password = "senha-segura-123")
    {
        var email = $"real-{Guid.NewGuid():N}@teste.com";
        var registered = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var auth = await DataAsync(registered);
        return (ClientFor(factory, auth), auth, email);
    }

    [Fact]
    public async Task DeleteAccount_RemovesTheAccountAndEverythingInIt_AndTheSessionStopsWorking()
    {
        var (client, auth, email) = await RegisterAsync(_factory);
        var folder = await DataAsync(await client.PostAsJsonAsync("/api/folders", new { name = "Trabalho" }));
        await client.PostAsJsonAsync("/api/notes", new { title = "Uma nota", content = "", folderId = folder.GetProperty("id").GetInt32() });
        Assert.Equal((1, 1, 1), await CountsForAsync(email));

        var response = await client.PostAsJsonAsync("/api/auth/delete-account", new { password = "senha-segura-123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((0, 0, 0), await CountsForAsync(email));

        // Sem como renovar a sessão nem entrar de novo com a mesma conta
        var refresh = await _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "senha-segura-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithAWrongPassword_Is400WithTheInvalidPasswordCode_AndNothingIsDeleted()
    {
        var (client, _, email) = await RegisterAsync(_factory);
        await client.PostAsJsonAsync("/api/notes", new { title = "Fica", content = "" });

        var response = await client.PostAsJsonAsync("/api/auth/delete-account", new { password = "senha-errada-123" });

        // 400, não 401: um 401 faria o front achar que a sessão caiu
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_password", body.GetProperty("errors").GetProperty("code")[0].GetString());
        Assert.Equal((1, 1, 0), await CountsForAsync(email));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutABody_IsRefusedForARegularAccount()
    {
        var (client, _, email) = await RegisterAsync(_factory);

        var response = await client.PostAsync("/api/auth/delete-account", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal((1, 0, 0), await CountsForAsync(email));
    }

    [Fact]
    public async Task DeleteAccount_RequiresALoggedInUser()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/delete-account", new { password = "senha-segura-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_OfADemo_NeedsNoPassword_AndLeavesOtherAccountsAlone()
    {
        var demo = await StartDemoAsync(_factory);
        var demoEmail = demo.GetProperty("user").GetProperty("email").GetString()!;
        var otherDemoEmail = (await StartDemoAsync(_factory)).GetProperty("user").GetProperty("email").GetString()!;
        var (_, _, realEmail) = await RegisterAsync(_factory);

        var response = await ClientFor(_factory, demo).PostAsync("/api/auth/delete-account", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((0, 0, 0), await CountsForAsync(demoEmail));
        Assert.Equal((1, 3, 1), await CountsForAsync(otherDemoEmail));
        Assert.Equal((1, 0, 0), await CountsForAsync(realEmail));
    }

    [Fact]
    public async Task DeleteAccount_DoesNotTouchOtherAccounts()
    {
        var (first, _, firstEmail) = await RegisterAsync(_factory);
        var (second, _, secondEmail) = await RegisterAsync(_factory);
        await second.PostAsJsonAsync("/api/notes", new { title = "Da outra pessoa", content = "" });

        await first.PostAsJsonAsync("/api/auth/delete-account", new { password = "senha-segura-123" });

        Assert.Equal((0, 0, 0), await CountsForAsync(firstEmail));
        Assert.Equal((1, 1, 0), await CountsForAsync(secondEmail));
    }

    [Fact]
    public async Task DeleteAccount_IsRateLimitedLikeTheOtherAuthRoutes()
    {
        using var factory = new RateLimitedInMemoryApiFactory();
        var (client, _, _) = await RegisterAsync(factory);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/delete-account", new { password = "errada-123456" })).StatusCode);

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
