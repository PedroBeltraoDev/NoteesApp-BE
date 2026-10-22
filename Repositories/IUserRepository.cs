using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail);
    Task<User?> GetByIdAsync(Guid id);
    Task AddAsync(User user);
    Task UpdateAsync(User user);

    /// <summary>Grava a conta demo e o conteúdo de exemplo de uma vez só.</summary>
    Task AddDemoAsync(User user, IEnumerable<Folder> folders, IEnumerable<Note> notes);

    Task<int> CountActiveDemosAsync(DateTime now);

    /// <summary>Apaga as demos vencidas com tudo o que é delas (notas, pastas, tokens). Devolve quantas.</summary>
    Task<int> DeleteExpiredDemosAsync(DateTime now);

    /// <summary>Apaga a conta com tudo o que é dela (notas, pastas, tokens).</summary>
    Task DeleteAsync(Guid userId);

    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash);

    /// <summary>Grava o token e remove os tokens já expirados do mesmo usuário.</summary>
    Task AddRefreshTokenAsync(RefreshToken token, DateTime now);

    /// <summary>Revoga o token antigo e grava o novo na mesma transação.</summary>
    Task RotateRefreshTokenAsync(RefreshToken old, RefreshToken replacement, DateTime now);

    Task RevokeRefreshTokenAsync(RefreshToken token, DateTime now);
    Task RevokeAllRefreshTokensAsync(Guid userId, DateTime now);
}
