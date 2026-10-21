using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

// Toda consulta é restrita ao dono (userId): uma nota de outro usuário simplesmente não existe
public interface INoteRepository
{
    Task<IEnumerable<Note>> GetAllAsync(Guid userId);
    Task<Note?> GetByIdAsync(Guid userId, int id);
    // folderId filtra pela pasta; folder (nome) continua aceito para clientes antigos
    Task<IEnumerable<Note>> GetFilteredAsync(Guid userId, int? folderId, string? folder, string? tag);
    Task<PagedResult<Note>> GetPagedAsync(Guid userId, int? folderId, string? folder, string? tag, int page, int pageSize);
    Task<Note> CreateAsync(Note note);
    Task UpdateAsync(Note note);
    Task DeleteAsync(Guid userId, int id);
    Task<IEnumerable<string>> GetDistinctFoldersAsync(Guid userId);
    Task<IEnumerable<string>> GetDistinctTagsAsync(Guid userId);
}
