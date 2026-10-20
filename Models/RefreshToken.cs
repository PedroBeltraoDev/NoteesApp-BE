namespace NotesApp.Api.Models;

public class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid UserId { get; init; }

    // SHA-256 (hex) do token entregue ao cliente; o token em si nunca é guardado
    public string TokenHash { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime? RevokedAt { get; set; }
}
