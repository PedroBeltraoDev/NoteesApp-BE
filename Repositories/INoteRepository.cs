using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

public interface INoteRepository
{
    Task<IEnumerable<Note>> GetAllAsync();
    Task<Note?> GetByIdAsync(int id);
    Task<IEnumerable<Note>> GetByFolderAsync(string folder);
    Task<IEnumerable<Note>> GetByTagAsync(string tag);
    Task<IEnumerable<Note>> GetFilteredAsync(string? folder, string? tag);
    Task<Note> CreateAsync(Note note);
    Task UpdateAsync(Note note);
    Task DeleteAsync(int id);
    Task<IEnumerable<string>> GetDistinctFoldersAsync();
    Task<IEnumerable<string>> GetDistinctTagsAsync();
}