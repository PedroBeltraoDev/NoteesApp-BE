using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly FixedTimeProvider _time = new();
    private readonly PasswordHasher<User> _hasher = new();
    private readonly TokenService _tokens;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _tokens = new TokenService(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions { Key = TestJwt.Key }), _time);

        _service = new AuthService(_users.Object, _tokens, _hasher, _time, NullLogger<AuthService>.Instance);
    }

    private User ExistingUser(string email = "pessoa@teste.com", string password = "senha-segura-123")
    {
        var user = new User { Email = email };
        user.PasswordHash = _hasher.HashPassword(user, password);
        return user;
    }

    private RefreshToken StoredToken(User user, string token, DateTime? expiresAt = null, DateTime? revokedAt = null) => new()
    {
        UserId = user.Id,
        TokenHash = _tokens.HashRefreshToken(token),
        CreatedAt = _time.Now.UtcDateTime,
        ExpiresAt = expiresAt ?? _time.Now.UtcDateTime.AddDays(1),
        RevokedAt = revokedAt
    };

    // ---------- Registro

    [Fact]
    public async Task Register_StoresNormalizedEmailAndAHashedPassword()
    {
        User? saved = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>())).Callback<User>(u => saved = u).Returns(Task.CompletedTask);

        await _service.RegisterAsync(new RegisterDto { Email = "  Pessoa@Teste.COM ", Password = "senha-segura-123" });

        Assert.NotNull(saved);
        Assert.Equal("pessoa@teste.com", saved.Email);
        Assert.NotEqual("senha-segura-123", saved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(saved, saved.PasswordHash, "senha-segura-123"));
    }

    [Fact]
    public async Task Register_StoresTheRefreshTokenOnlyAsAHash()
    {
        RefreshToken? stored = null;
        _users.Setup(u => u.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<DateTime>()))
              .Callback<RefreshToken, DateTime>((t, _) => stored = t)
              .Returns(Task.CompletedTask);

        var response = await _service.RegisterAsync(new RegisterDto { Email = "a@teste.com", Password = "senha-segura-123" });

        Assert.NotNull(stored);
        Assert.NotEqual(response.RefreshToken, stored.TokenHash);
        Assert.Equal(_tokens.HashRefreshToken(response.RefreshToken), stored.TokenHash);
    }

    [Fact]
    public async Task Register_WhenEmailExists_ThrowsConflictAndNeverSaves()
    {
        _users.Setup(u => u.GetByEmailAsync("pessoa@teste.com")).ReturnsAsync(ExistingUser());

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RegisterAsync(new RegisterDto { Email = "PESSOA@teste.com", Password = "senha-segura-123" }));

        _users.Verify(u => u.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_WhenUniqueIndexRejectsARacingInsert_ThrowsConflict()
    {
        var calls = 0;
        _users.Setup(u => u.GetByEmailAsync("pessoa@teste.com"))
              .ReturnsAsync(() => ++calls == 1 ? null : ExistingUser());
        _users.Setup(u => u.AddAsync(It.IsAny<User>())).ThrowsAsync(new DbUpdateException("duplicate key"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RegisterAsync(new RegisterDto { Email = "pessoa@teste.com", Password = "senha-segura-123" }));
    }

    // ---------- Login

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokens()
    {
        var user = ExistingUser();
        _users.Setup(u => u.GetByEmailAsync(user.Email)).ReturnsAsync(user);

        var response = await _service.LoginAsync(new LoginDto { Email = " PESSOA@teste.com", Password = "senha-segura-123" });

        Assert.Equal(user.Id, response.User.Id);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        _users.Verify(u => u.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsUnauthorizedAndIssuesNothing()
    {
        var user = ExistingUser();
        _users.Setup(u => u.GetByEmailAsync(user.Email)).ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginDto { Email = user.Email, Password = "errada-123456" }));

        Assert.Equal("E-mail ou senha inválidos", ex.Message);
        _users.Verify(u => u.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsTheSameUnauthorizedMessage()
    {
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginDto { Email = "ninguem@teste.com", Password = "qualquer-coisa-123" }));

        Assert.Equal("E-mail ou senha inválidos", ex.Message);
    }

    // ---------- Refresh

    [Fact]
    public async Task Refresh_WithValidToken_RotatesIt()
    {
        var user = ExistingUser();
        var stored = StoredToken(user, "token-atual");
        _users.Setup(u => u.GetRefreshTokenAsync(stored.TokenHash)).ReturnsAsync(stored);
        _users.Setup(u => u.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var response = await _service.RefreshAsync("token-atual");

        Assert.NotEqual("token-atual", response.RefreshToken);
        _users.Verify(u => u.RotateRefreshTokenAsync(
            stored,
            It.Is<RefreshToken>(t => t.TokenHash == _tokens.HashRefreshToken(response.RefreshToken) && t.UserId == user.Id),
            It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesEverySessionOfTheUser()
    {
        var user = ExistingUser();
        var stored = StoredToken(user, "token-usado", revokedAt: _time.Now.UtcDateTime.AddMinutes(-1));
        _users.Setup(u => u.GetRefreshTokenAsync(stored.TokenHash)).ReturnsAsync(stored);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("token-usado"));

        _users.Verify(u => u.RevokeAllRefreshTokensAsync(user.Id, It.IsAny<DateTime>()), Times.Once);
        _users.Verify(u => u.RotateRefreshTokenAsync(
            It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ThrowsUnauthorized()
    {
        var user = ExistingUser();
        var stored = StoredToken(user, "token-velho", expiresAt: _time.Now.UtcDateTime.AddSeconds(-1));
        _users.Setup(u => u.GetRefreshTokenAsync(stored.TokenHash)).ReturnsAsync(stored);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("token-velho"));

        _users.Verify(u => u.RotateRefreshTokenAsync(
            It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ThrowsUnauthorized()
    {
        _users.Setup(u => u.GetRefreshTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("desconhecido"));
    }

    [Fact]
    public async Task Refresh_WhenTheUserWasDeleted_ThrowsUnauthorized()
    {
        var user = ExistingUser();
        var stored = StoredToken(user, "token-atual");
        _users.Setup(u => u.GetRefreshTokenAsync(stored.TokenHash)).ReturnsAsync(stored);
        _users.Setup(u => u.GetByIdAsync(user.Id)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.RefreshAsync("token-atual"));
    }

    // ---------- Logout

    [Fact]
    public async Task Logout_WithUnknownToken_DoesNothingAndDoesNotThrow()
    {
        _users.Setup(u => u.GetRefreshTokenAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        await _service.LogoutAsync("desconhecido");

        _users.Verify(u => u.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Logout_RevokesOnlyThePresentedToken()
    {
        var user = ExistingUser();
        var stored = StoredToken(user, "token-atual");
        _users.Setup(u => u.GetRefreshTokenAsync(stored.TokenHash)).ReturnsAsync(stored);

        await _service.LogoutAsync("token-atual");

        _users.Verify(u => u.RevokeRefreshTokenAsync(stored, It.IsAny<DateTime>()), Times.Once);
        _users.Verify(u => u.RevokeAllRefreshTokensAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }
}
