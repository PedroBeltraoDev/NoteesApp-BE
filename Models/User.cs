namespace NotesApp.Api.Models;

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // Sempre normalizado (trim + minúsculas); único no banco
    public string Email { get; init; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // Conta de demonstração: temporária (vale até DemoExpiresAt) e sem senha utilizável
    public bool IsDemo { get; init; }

    public DateTime? DemoExpiresAt { get; init; }
}
