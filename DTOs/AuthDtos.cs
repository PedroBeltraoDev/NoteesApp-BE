using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "E-mail é obrigatório")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    [MaxLength(254, ErrorMessage = "E-mail deve ter no máximo 254 caracteres")]
    public string Email { get; set; } = string.Empty;

    // O teto evita que senhas gigantes sejam usadas para sobrecarregar o hash
    [Required(ErrorMessage = "Senha é obrigatória")]
    [MinLength(8, ErrorMessage = "Senha deve ter pelo menos 8 caracteres")]
    [MaxLength(128, ErrorMessage = "Senha deve ter no máximo 128 caracteres")]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required(ErrorMessage = "E-mail é obrigatório")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Senha é obrigatória")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequestDto
{
    [Required(ErrorMessage = "refreshToken é obrigatório")]
    [MaxLength(256)]
    public string RefreshToken { get; set; } = string.Empty;
}

// Confirmação de senha para excluir a conta; a conta demo não tem senha e pode mandar o corpo vazio
public class DeleteAccountDto
{
    [MaxLength(128)]
    public string? Password { get; set; }
}

// IsDemo/DemoExpiresAt: contas de demonstração (temporárias); DemoExpiresAt é o fim do prazo, em UTC
public record UserDto(Guid Id, string Email, bool IsDemo = false, DateTime? DemoExpiresAt = null);

public record AuthResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
