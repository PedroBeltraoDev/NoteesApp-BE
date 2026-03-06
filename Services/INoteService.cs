using NotesApp.Api.DTOs;
using NotesApp.Api.Models;

namespace NotesApp.Api.Services;

public interface INoteService
{
    Task<IEnumerable<Note>> GetAllNotesAsync();
    Task<Note?> GetNoteByIdAsync(int id);
    Task<IEnumerable<Note>> GetNotesByFolderAsync(string folder);
    Task<IEnumerable<Note>> GetNotesByTagAsync(string tag);
    Task<IEnumerable<Note>> GetFilteredNotesAsync(string? folder, string? tag);
    Task<IEnumerable<Note>> SearchNotesAsync(string query);
    Task<Note> CreateNoteAsync(CreateNoteDto dto);
    Task UpdateNoteAsync(int id, UpdateNoteDto dto);
    Task DeleteNoteAsync(int id);
    Task<IEnumerable<string>> GetAvailableFoldersAsync();
    Task<IEnumerable<string>> GetAvailableTagsAsync();
}