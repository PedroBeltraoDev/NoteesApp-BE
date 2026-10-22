using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NotesApp.Api.DTOs;
using NotesApp.Api.Exceptions;
using Microsoft.Extensions.Options;
using NotesApp.Api.Models;
using NotesApp.Api.Options;
using NotesApp.Api.Repositories;

namespace NotesApp.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> CreateDemoAsync();
    Task<AuthResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
    Task DeleteAccountAsync(Guid userId, string? password);
}

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "E-mail ou senha inválidos";
    private const string InvalidRefreshTokenMessage = "Sessão inválida ou expirada. Faça login novamente";

    // Hash descartável: login de e-mail inexistente também paga o custo de verificar um hash,
    // para o tempo de resposta não revelar quais e-mails estão cadastrados.
    private static readonly Lazy<string> DummyHash = new(
        () => new PasswordHasher<User>().HashPassword(new User(), "senha-descartavel-para-igualar-tempo"));

    // Domínio reservado (.invalid nunca existe): identifica as contas demo e não pode ser usado em cadastros
    public const string DemoEmailDomain = "demo.noteesapp.invalid";

    // Não é um hash válido de nenhuma senha: a conta demo não tem senha e o login com ela é sempre recusado
    private const string UnusablePasswordHash = "!";

    private readonly DemoOptions _demo;
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
        IOptions<DemoOptions> demo,
        ILogger<AuthService> logger)
    {
        _demo = demo.Value;
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
        _time = time;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var email = NormalizeEmail(dto.Email);

        if (email.EndsWith("@" + DemoEmailDomain, StringComparison.Ordinal))
            throw new ArgumentException("E-mail inválido");

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

        // Conta demo não tem senha: trata como e-mail inexistente (inclusive no tempo de resposta)
        if (user is null || user.IsDemo)
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

        var user = await _users.GetByIdAsync(stored.UserId)
            ?? throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);

        // Antes de olhar a validade do token: o refresh da demo vence junto com ela, e o front precisa saber que foi a demo
        if (user.IsDemo && user.DemoExpiresAt <= now)
        {
            await _users.DeleteExpiredDemosAsync(now);
            throw new DemoExpiredException();
        }

        if (stored.ExpiresAt <= now)
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);

        var replacement = CapToDemo(user, _tokens.CreateRefreshToken());
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

        return ToDto(user);
    }

    /// <summary>
    /// Exclui a conta com tudo o que é dela (notas, pastas e sessões). Conta com senha exige a senha atual;
    /// a demo não tem senha e é apagada direto.
    /// </summary>
    public async Task DeleteAccountAsync(Guid userId, string? password)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado");

        if (!user.IsDemo)
        {
            var verified = !string.IsNullOrEmpty(password)
                && _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

            if (!verified)
            {
                _logger.LogWarning("Senha incorreta ao excluir conta - ID: {UserId}", user.Id);
                throw new InvalidPasswordException();
            }
        }

        await _users.DeleteAsync(user.Id);
        _logger.LogInformation("Conta excluída - ID: {UserId}", user.Id);
    }

    /// <summary>
    /// Cria uma conta de demonstração: sem senha, com conteúdo de exemplo, válida por <see cref="DemoOptions.Minutes"/>.
    /// Aproveita para apagar as demos vencidas (não há tarefa em segundo plano na hospedagem serverless).
    /// </summary>
    public async Task<AuthResponseDto> CreateDemoAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;

        await _users.DeleteExpiredDemosAsync(now);

        if (await _users.CountActiveDemosAsync(now) >= _demo.MaxActive)
            throw new DemoUnavailableException();

        var user = new User
        {
            Email = $"demo-{Guid.NewGuid():N}@{DemoEmailDomain}",
            PasswordHash = UnusablePasswordHash,
            CreatedAt = now,
            IsDemo = true,
            DemoExpiresAt = now.AddMinutes(_demo.Minutes)
        };

        var (folders, notes) = DemoSeed.Build(user, _demo.Minutes, now);
        await _users.AddDemoAsync(user, folders, notes);

        _logger.LogInformation("Conta demo criada - ID: {UserId}", user.Id);
        return await IssueTokensAsync(user);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(User user)
    {
        var refresh = CapToDemo(user, _tokens.CreateRefreshToken());
        await _users.AddRefreshTokenAsync(ToEntity(refresh, user.Id), refresh.CreatedAt);

        return BuildResponse(user, _tokens.CreateAccessToken(user), refresh);
    }

    private static AuthResponseDto BuildResponse(User user, AccessToken access, GeneratedRefreshToken refresh)
        => new(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, ToDto(user));

    private static UserDto ToDto(User user) => new(user.Id, user.Email, user.IsDemo, user.DemoExpiresAt);

    // O refresh token da demo também não passa do prazo dela
    private static GeneratedRefreshToken CapToDemo(User user, GeneratedRefreshToken refresh)
        => user.IsDemo && user.DemoExpiresAt is { } end && end < refresh.ExpiresAt ? refresh with { ExpiresAt = end } : refresh;

    private static RefreshToken ToEntity(GeneratedRefreshToken refresh, Guid userId) => new()
    {
        UserId = userId,
        TokenHash = refresh.Hash,
        CreatedAt = refresh.CreatedAt,
        ExpiresAt = refresh.ExpiresAt
    };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
