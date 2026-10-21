using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

// Toda consulta é restrita ao dono (userId): a pasta de outro usuário simplesmente não existe
public interface IFolderRepository
{
    Task<IReadOnlyList<FolderSummary>> GetAllAsync(Guid userId);
    Task<FolderSummary?> GetSummaryAsync(Guid userId, int id);
    Task<Folder?> GetByIdAsync(Guid userId, int id);
    Task<Folder?> GetByNameAsync(Guid userId, string name);
    Task<Folder> CreateAsync(Folder folder);
    Task UpdateAsync(Folder folder);

    // As notas da pasta ficam sem pasta; nenhuma nota é apagada
    Task DeleteAsync(Guid userId, int id);
}
