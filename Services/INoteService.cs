using NotesApp.Api.DTOs;
using NotesApp.Api.Models;

namespace NotesApp.Api.Services;

public interface INoteService
{
    Task<Note> GetByIdAsync(Guid userId, int id);
    Task<IEnumerable<Note>> GetAllAsync(Guid userId);
    Task<IEnumerable<Note>> GetFilteredAsync(Guid userId, int? folderId, string? folder, string? tag);
    Task<PagedResult<Note>> GetPagedAsync(Guid userId, int? folderId, string? folder, string? tag, int page, int pageSize);
    Task<Note> CreateAsync(Guid userId, CreateNoteDto dto);
    Task<Note> UpdateAsync(Guid userId, int id, UpdateNoteDto dto);
    Task DeleteAsync(Guid userId, int id);
    Task<IEnumerable<string>> GetDistinctFoldersAsync(Guid userId);
    Task<IEnumerable<string>> GetDistinctTagsAsync(Guid userId);
}