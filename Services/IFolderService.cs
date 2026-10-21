using NotesApp.Api.Models;

namespace NotesApp.Api.Services;

public interface IFolderService
{
    Task<IReadOnlyList<FolderSummary>> GetAllAsync(Guid userId);
    Task<FolderSummary> GetByIdAsync(Guid userId, int id);
    Task<FolderSummary> CreateAsync(Guid userId, string name);
    Task<FolderSummary> RenameAsync(Guid userId, int id, string name);
    Task DeleteAsync(Guid userId, int id);
}
