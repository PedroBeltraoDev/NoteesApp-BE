using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotesApp.Api.DTOs;
using NotesApp.Api.Exceptions;
using NotesApp.Api.Models;
using NotesApp.Api.Options;
using NotesApp.Api.Repositories;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

// Regras da conta demo no serviço (repositório simulado, relógio fixo)
public class DemoAccountTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly FixedTimeProvider _time = new();
    private readonly PasswordHasher<User> _hasher = new();
    private readonly TokenService _tokens;

    public DemoAccountTests()
    {
        _tokens = new TokenService(Microsoft.Extensions.Options.Options.Create(new JwtOptions { Key = TestJwt.Key }), _time);
    }

    private AuthService ServiceWith(DemoOptions? demo = null)
        => new(_users.Object, _tokens, _hasher, _time, Microsoft.Extensions.Options.Options.Create(demo ?? new DemoOptions()), NullLogger<AuthService>.Instance);

    private DateTime Now => _time.Now.UtcDateTime;

    private User DemoUser(DateTime expiresAt) => new()
    {
        Email = $"demo-x@{AuthService.DemoEmailDomain}",
        PasswordHash = "!",
        IsDemo = true,
        DemoExpiresAt = expiresAt
    };

    private RefreshToken StoredToken(User user, string token, DateTime expiresAt) => new()
    {
        UserId = user.Id,
        TokenHash = _tokens.HashRefreshToken(token),
        CreatedAt = Now,
        ExpiresAt = expiresAt
    };

    // ---------- Criar a demo

    [Fact]
    public async Task CreateDemo_MakesAnExpiringAccountWithoutAPassword()
    {
        User? saved = null;
        _users.Setup(u => u.AddDemoAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Folder>>(), It.IsAny<IEnumerable<Note>>()))
              .Callback<User, IEnumerable<Folder>, IEnumerable<Note>>((user, _, _) => saved = user)
              .Returns(Task.CompletedTask);

        var response = await ServiceWith().CreateDemoAsync();

        Assert.NotNull(saved);
        Assert.True(saved.IsDemo);
        Assert.Equal(Now.AddMinutes(30), saved.DemoExpiresAt);
        Assert.EndsWith("@" + AuthService.DemoEmailDomain, saved.Email);
        Assert.Equal("!", saved.PasswordHash); // não é hash de senha nenhuma
        Assert.True(response.User.IsDemo);
        Assert.Equal(Now.AddMinutes(30), response.User.DemoExpiresAt);
    }

    [Fact]
    public async Task CreateDemo_EachCallMakesADifferentAccount()
    {
        var emails = new List<string>();
        _users.Setup(u => u.AddDemoAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Folder>>(), It.IsAny<IEnumerable<Note>>()))
              .Callback<User, IEnumerable<Folder>, IEnumerable<Note>>((user, _, _) => emails.Add(user.Email))
              .Returns(Task.CompletedTask);
        var service = ServiceWith();

        await service.CreateDemoAsync();
        await service.CreateDemoAsync();

        Assert.Equal(2, emails.Distinct().Count());
    }

    [Fact]
    public async Task CreateDemo_BringsExampleContentOwnedByTheDemoUser()
    {
        User? owner = null;
        List<Folder>? folders = null;
        List<Note>? loose = null;
        _users.Setup(u => u.AddDemoAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Folder>>(), It.IsAny<IEnumerable<Note>>()))
              .Callback<User, IEnumerable<Folder>, IEnumerable<Note>>((user, f, n) => { owner = user; folders = f.ToList(); loose = n.ToList(); })
              .Returns(Task.CompletedTask);

        await ServiceWith().CreateDemoAsync();

        var folder = Assert.Single(folders!);
        Assert.Equal("Exemplos", folder.Name);
        Assert.Equal(2, folder.Notes.Count);
        Assert.Single(loose!);
        Assert.All(folders!.SelectMany(f => f.Notes).Concat(loose!), n => Assert.Equal(owner!.Id, n.UserId));
        Assert.Equal(owner!.Id, folder.UserId);
        Assert.Contains(folder.Notes, n => n.IsPinned);
        Assert.Contains("30 minutos", folder.Notes.First(n => n.IsPinned).Content);
    }

    [Fact]
    public async Task CreateDemo_TheRefreshTokenDoesNotOutliveTheDemo()
    {
        var response = await ServiceWith().CreateDemoAsync();

        // O refresh normal dura 14 dias, mas o da demo morre com ela
        Assert.Equal(Now.AddMinutes(30), response.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task CreateDemo_ADemoShorterThanTheAccessTokenCapsTheAccessToken()
    {
        var response = await ServiceWith(new DemoOptions { Minutes = 5 }).CreateDemoAsync();

        Assert.Equal(Now.AddMinutes(5), response.AccessTokenExpiresAt); // sem a trava seriam 15 min
        Assert.Equal(Now.AddMinutes(5), response.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task CreateDemo_AccessTokenOfALongDemoKeepsItsNormalLifetime()
    {
        var response = await ServiceWith().CreateDemoAsync();

        Assert.Equal(Now.AddMinutes(15), response.AccessTokenExpiresAt);
    }

    // ---------- Limpeza e teto

    [Fact]
    public async Task CreateDemo_DeletesExpiredDemosFirst()
    {
        var order = new List<string>();
        _users.Setup(u => u.DeleteExpiredDemosAsync(It.IsAny<DateTime>())).Callback(() => order.Add("limpa")).ReturnsAsync(2);
        _users.Setup(u => u.CountActiveDemosAsync(It.IsAny<DateTime>())).Callback(() => order.Add("conta")).ReturnsAsync(0);
        _users.Setup(u => u.AddDemoAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Folder>>(), It.IsAny<IEnumerable<Note>>()))
              .Callback(() => order.Add("cria")).Returns(Task.CompletedTask);

        await ServiceWith().CreateDemoAsync();

        Assert.Equal(["limpa", "conta", "cria"], order);
        _users.Verify(u => u.DeleteExpiredDemosAsync(Now), Times.Once);
    }

    [Fact]
    public async Task CreateDemo_WhenTooManyDemosAreActive_RefusesAndSavesNothing()
    {
        _users.Setup(u => u.CountActiveDemosAsync(It.IsAny<DateTime>())).ReturnsAsync(2);

        await Assert.ThrowsAsync<DemoUnavailableException>(() => ServiceWith(new DemoOptions { MaxActive = 2 }).CreateDemoAsync());

        _users.Verify(u => u.AddDemoAsync(It.IsAny<User>(), It.IsAny<IEnumerable<Folder>>(), It.IsAny<IEnumerable<Note>>()), Times.Never);
    }

    [Fact]
    public async Task CreateDemo_BelowTheLimitStillWorks()
    {
        _users.Setup(u => u.CountActiveDemosAsync(It.IsAny<DateTime>())).ReturnsAsync(1);

        var response = await ServiceWith(new DemoOptions { MaxActive = 2 }).CreateDemoAsync();

        Assert.True(response.User.IsDemo);
    }

    // ---------- Quem não pode usar a demo como conta normal

    [Fact]
    public async Task Login_ADemoAccountCanNeverLogInWithAPassword()
    {
        var demo = DemoUser(Now.AddMinutes(10));
        _users.Setup(u => u.GetByEmailAsync(demo.Email)).ReturnsAsync(demo);

        // Qualquer senha, inclusive "!": é recusada como credencial inválida, sem estourar o verificador de hash
        foreach (var password in new[] { "!", "senha-qualquer-123", demo.PasswordHash })
        {
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => ServiceWith().LoginAsync(new LoginDto { Email = demo.Email, Password = password }));
            Assert.Equal("E-mail ou senha inválidos", exception.Message);
        }
    }

    [Fact]
    public async Task Register_CannotTakeTheReservedDemoDomain()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => ServiceWith().RegisterAsync(new RegisterDto { Email = $"Alguem@{AuthService.DemoEmailDomain}", Password = "senha-segura-123" }));

        Assert.Equal("E-mail inválido", exception.Message);
        _users.Verify(u => u.AddAsync(It.IsAny<User>()), Times.Never);
    }

    // ---------- Expirar

    [Fact]
    public async Task Refresh_AfterTheDemoEnds_ReportsDemoExpiredAndDeletesTheData()
    {
        var demo = DemoUser(Now.AddMinutes(-1));
        // O refresh da demo vence junto com ela: mesmo assim o motivo informado é "demo expirou", não "sessão inválida"
        _users.Setup(u => u.GetRefreshTokenAsync(_tokens.HashRefreshToken("r1"))).ReturnsAsync(StoredToken(demo, "r1", Now.AddMinutes(-1)));
        _users.Setup(u => u.GetByIdAsync(demo.Id)).ReturnsAsync(demo);

        var exception = await Assert.ThrowsAsync<DemoExpiredException>(() => ServiceWith().RefreshAsync("r1"));

        Assert.Equal("O tempo da conta demo acabou", exception.Message);
        Assert.IsAssignableFrom<UnauthorizedAccessException>(exception); // continua sendo um 401
        _users.Verify(u => u.DeleteExpiredDemosAsync(Now), Times.Once);
        _users.Verify(u => u.RotateRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_DuringTheDemo_RenewsButStillStopsAtTheDemoEnd()
    {
        var demo = DemoUser(Now.AddMinutes(10));
        _users.Setup(u => u.GetRefreshTokenAsync(_tokens.HashRefreshToken("r1"))).ReturnsAsync(StoredToken(demo, "r1", Now.AddMinutes(10)));
        _users.Setup(u => u.GetByIdAsync(demo.Id)).ReturnsAsync(demo);
        RefreshToken? replacement = null;
        _users.Setup(u => u.RotateRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<DateTime>()))
              .Callback<RefreshToken, RefreshToken, DateTime>((_, r, _) => replacement = r).Returns(Task.CompletedTask);

        var response = await ServiceWith().RefreshAsync("r1");

        Assert.Equal(Now.AddMinutes(10), replacement!.ExpiresAt);
        Assert.Equal(Now.AddMinutes(10), response.RefreshTokenExpiresAt);
        Assert.Equal(Now.AddMinutes(10), response.AccessTokenExpiresAt); // 15 min normais, mas faltam 10 de demo
        _users.Verify(u => u.DeleteExpiredDemosAsync(It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_OfARegularAccountIsNotAffected()
    {
        var user = new User { Email = "pessoa@teste.com", PasswordHash = "x" };
        _users.Setup(u => u.GetRefreshTokenAsync(_tokens.HashRefreshToken("r1"))).ReturnsAsync(StoredToken(user, "r1", Now.AddDays(3)));
        _users.Setup(u => u.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var response = await ServiceWith().RefreshAsync("r1");

        Assert.False(response.User.IsDemo);
        Assert.Null(response.User.DemoExpiresAt);
        Assert.Equal(Now.AddDays(14), response.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task Me_TellsWhetherTheAccountIsADemoAndWhenItEnds()
    {
        var demo = DemoUser(Now.AddMinutes(7));
        _users.Setup(u => u.GetByIdAsync(demo.Id)).ReturnsAsync(demo);

        var me = await ServiceWith().GetCurrentUserAsync(demo.Id);

        Assert.True(me.IsDemo);
        Assert.Equal(Now.AddMinutes(7), me.DemoExpiresAt);
    }
}
