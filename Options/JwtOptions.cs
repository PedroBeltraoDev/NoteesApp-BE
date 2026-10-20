using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NotesApp.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    // Chave de assinatura HS256 (mínimo de 32 caracteres). Vem de configuração/variável de ambiente
    // (Jwt__Key); nunca de código. A API não sobe sem ela.
    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "NoteesApp.Api";

    [Required]
    public string Audience { get; set; } = "NoteesApp.Client";

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}
