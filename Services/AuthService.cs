using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NotesApp.Api.DTOs;
using NotesApp.Api.Exceptions;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;

namespace NotesApp.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "E-mail ou senha inválidos";
    private const string InvalidRefreshTokenMessage = "Sessão inválida ou expirada. Faça login novamente";

    // Hash descartável: login de e-mail inexistente também paga o custo de verificar um hash,
    // para o tempo de resposta não revelar quais e-mails estão cadastrados.
    private static readonly Lazy<string> DummyHash = new(
        () => new PasswordHasher<User>().HashPassword(new User(), "senha-descartavel-para-igualar-tempo"));

    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher<User> _hasher;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        ITokenService tokens,
        IPasswordHasher<User> hasher,
        TimeProvider time,
        ILogger<AuthService> logger)
    {
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
        _time = time;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var email = NormalizeEmail(dto.Email);

        if (await _users.GetByEmailAsync(email) is not null)
            throw new ConflictException("E-mail já cadastrado");

        var user = new User { Email = email, CreatedAt = _time.GetUtcNow().UtcDateTime };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);

        try
        {
            await _users.AddAsync(user);
        }
        catch (DbUpdateException)
        {
            // Duas inscrições simultâneas com o mesmo e-mail: o índice único barrou a segunda
            if (await _users.GetByEmailAsync(email) is not null)
                throw new ConflictException("E-mail já cadastrado");

            throw;
        }

        _logger.LogInformation("Usuário registrado - ID: {UserId}", user.Id);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _users.GetByEmailAsync(NormalizeEmail(dto.Email));

        if (user is null)
        {
            _hasher.VerifyHashedPassword(new User(), DummyHash.Value, dto.Password);
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Falha de login - ID: {UserId}", user.Id);
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);
            await _users.UpdateAsync(user);
        }

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponseDto> RefreshAsync(string refreshToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var stored = await _users.GetRefreshTokenAsync(_tokens.HashRefreshToken(refreshToken));

        if (stored is null)
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);

        if (stored.RevokedAt is not null)
        {
            // Token já usado/revogado sendo apresentado de novo: pode ter sido roubado.
            // Derruba todas as sessões do usuário.
            _logger.LogWarning("Reuso de refresh token detectado - ID: {UserId}", stored.UserId);
            await _users.RevokeAllRefreshTokensAsync(stored.UserId, now);
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);
        }

        if (stored.ExpiresAt <= now)
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);

        var user = await _users.GetByIdAsync(stored.UserId)
            ?? throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);

        var replacement = _tokens.CreateRefreshToken();
        await _users.RotateRefreshTokenAsync(stored, ToEntity(replacement, user.Id), now);

        return BuildResponse(user, _tokens.CreateAccessToken(user), replacement);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await _users.GetRefreshTokenAsync(_tokens.HashRefreshToken(refreshToken));

        // Idempotente: token desconhecido ou já revogado não é erro
        if (stored is not null)
            await _users.RevokeRefreshTokenAsync(stored, _time.GetUtcNow().UtcDateTime);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado");

        return new UserDto(user.Id, user.Email);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(User user)
    {
        var refresh = _tokens.CreateRefreshToken();
        await _users.AddRefreshTokenAsync(ToEntity(refresh, user.Id), refresh.CreatedAt);

        return BuildResponse(user, _tokens.CreateAccessToken(user), refresh);
    }

    private static AuthResponseDto BuildResponse(User user, AccessToken access, GeneratedRefreshToken refresh)
        => new(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, new UserDto(user.Id, user.Email));

    private static RefreshToken ToEntity(GeneratedRefreshToken refresh, Guid userId) => new()
    {
        UserId = userId,
        TokenHash = refresh.Hash,
        CreatedAt = refresh.CreatedAt,
        ExpiresAt = refresh.ExpiresAt
    };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
