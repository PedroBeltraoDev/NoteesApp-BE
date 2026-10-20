using NotesApp.Api.DTOs;
using NotesApp.Api.Models;

namespace NotesApp.Api.Services;

public interface INoteService
{
    Task<Note> GetByIdAsync(int id);
    Task<IEnumerable<Note>> GetAllAsync();
    Task<IEnumerable<Note>> GetFilteredAsync(string? folder, string? tag);
    Task<Note> CreateAsync(CreateNoteDto dto);
    Task<Note> UpdateAsync(int id, UpdateNoteDto dto);
    Task DeleteAsync(int id);
    Task<IEnumerable<string>> GetDistinctFoldersAsync();
    Task<IEnumerable<string>> GetDistinctTagsAsync();
}