using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotesApp.Api.Models;
using NotesApp.Api.Options;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

public class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

public class TokenServiceTests
{
    private readonly FixedTimeProvider _time = new();
    private readonly JwtOptions _options = new()
    {
        Key = TestJwt.Key,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 14
    };

    private TokenService CreateService() => new(Microsoft.Extensions.Options.Options.Create(_options), _time);

    private static User NewUser() => new() { Email = "pessoa@teste.com" };

    [Fact]
    public async Task CreateAccessToken_ContainsSubjectEmailIssuerAudienceAndExpiry()
    {
        var user = NewUser();

        var access = CreateService().CreateAccessToken(user);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(access.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = _options.CreateSigningKey(),
            ValidateLifetime = false
        });

        Assert.True(result.IsValid);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.Email, jwt.GetClaim("email").Value);
        Assert.Equal(_time.Now.UtcDateTime.AddMinutes(15), access.ExpiresAt);
        Assert.Equal(access.ExpiresAt, jwt.ValidTo);
    }

    [Fact]
    public void CreateAccessToken_NeverContainsThePasswordHash()
    {
        var user = NewUser();
        user.PasswordHash = "hash-secreto";

        var token = CreateService().CreateAccessToken(user).Token;
        var payload = new JsonWebToken(token).EncodedPayload;
        var decoded = System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(payload));

        Assert.DoesNotContain("hash-secreto", decoded);
        Assert.DoesNotContain("password", decoded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateAccessToken_UsesHmacSha256()
    {
        var token = new JsonWebToken(CreateService().CreateAccessToken(NewUser()).Token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Alg);
    }

    [Fact]
    public void CreateAccessToken_AfterExpiry_IsRejectedByValidation()
    {
        var access = CreateService().CreateAccessToken(NewUser());
        _time.Now = _time.Now.AddMinutes(16);

        var handler = new JsonWebTokenHandler();
        var result = handler.ValidateTokenAsync(access.Token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = _options.CreateSigningKey(),
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (_, expires, _, _) => expires > _time.Now.UtcDateTime
        }).GetAwaiter().GetResult();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateRefreshToken_IsRandomLongAndExpiresAtTheConfiguredTime()
    {
        var service = CreateService();

        var a = service.CreateRefreshToken();
        var b = service.CreateRefreshToken();

        Assert.NotEqual(a.Token, b.Token);
        Assert.True(a.Token.Length >= 80);
        Assert.Equal(_time.Now.UtcDateTime, a.CreatedAt);
        Assert.Equal(_time.Now.UtcDateTime.AddDays(14), a.ExpiresAt);
    }

    [Fact]
    public void CreateRefreshToken_StoresOnlyAHashOfTheToken()
    {
        var service = CreateService();

        var refresh = service.CreateRefreshToken();

        Assert.NotEqual(refresh.Token, refresh.Hash);
        Assert.Equal(64, refresh.Hash.Length);
        Assert.Equal(refresh.Hash, service.HashRefreshToken(refresh.Token));
        Assert.NotEqual(refresh.Hash, service.HashRefreshToken(refresh.Token + "x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    [InlineData("31-caracteres-aaaaaaaaaaaaaaaa")]
    public void JwtOptions_RejectsMissingOrShortKeys(string key)
    {
        var options = new JwtOptions { Key = key };
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);

        Assert.False(valid);
    }

    [Fact]
    public void JwtOptions_AcceptsA32CharacterKey()
    {
        var options = new JwtOptions { Key = new string('k', 32) };
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, context, new List<System.ComponentModel.DataAnnotations.ValidationResult>(), true);

        Assert.True(valid);
    }
}
