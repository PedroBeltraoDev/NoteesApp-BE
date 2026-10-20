using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotesApp.Api.Models;
using NotesApp.Api.Options;

namespace NotesApp.Api.Services;

public record AccessToken(string Token, DateTime ExpiresAt);

public record GeneratedRefreshToken(string Token, string Hash, DateTime CreatedAt, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    GeneratedRefreshToken CreateRefreshToken();
    string HashRefreshToken(string token);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _time;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
        _credentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(User user)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }

    public GeneratedRefreshToken CreateRefreshToken()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

        return new GeneratedRefreshToken(token, HashRefreshToken(token), now, now.AddDays(_options.RefreshTokenDays));
    }

    // Só o hash vai para o banco: um vazamento da tabela não revela tokens utilizáveis
    public string HashRefreshToken(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
