using NotesApp.Api.DTOs;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;

namespace NotesApp.Api.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _repository;
    
    public NoteService(INoteRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<IEnumerable<Note>> GetAllNotesAsync()
    {
        return await _repository.GetAllAsync();
    }
    
    public async Task<Note?> GetNoteByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }
    
    public async Task<IEnumerable<Note>> GetNotesByFolderAsync(string folder)
    {
        return await _repository.GetByFolderAsync(folder);
    }
    
    public async Task<IEnumerable<Note>> GetNotesByTagAsync(string tag)
    {
        return await _repository.GetByTagAsync(tag);
    }
    
    public async Task<IEnumerable<Note>> GetFilteredNotesAsync(string? folder, string? tag)
    {
        return await _repository.GetFilteredAsync(folder, tag);
    }
    
    public async Task<IEnumerable<Note>> SearchNotesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllNotesAsync();
        }
        
        var allNotes = await GetAllNotesAsync();
        return allNotes.Where(n => 
            n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || 
            n.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
    
    public async Task<Note> CreateNoteAsync(CreateNoteDto dto)
    {
        // Validação de negócio
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new ArgumentException("Title is required");
        }
    
        if (dto.Title.Length < 3)
        {
            throw new ArgumentException("Title must be at least 3 characters");
        }
    
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            throw new ArgumentException("Content is required");
        }
    
        if (dto.Content.Length < 10)
        {
            throw new ArgumentException("Content must be at least 10 characters");
        }
    
        var note = new Note
        {
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            Tags = dto.Tags ?? new List<string>(),
            Folder = dto.Folder,
            CreatedAt = DateTime.UtcNow, 
            IsPinned = false
        };
    
        return await _repository.CreateAsync(note);
    }
    
    public async Task UpdateNoteAsync(int id, UpdateNoteDto dto)
    {
        // Validações...
    
        var existingNote = await _repository.GetByIdAsync(id);
    
        if (existingNote == null)
        {
            throw new KeyNotFoundException($"Note with id {id} not found");
        }
    
        existingNote.Title = dto.Title.Trim();
        existingNote.Content = dto.Content.Trim();
        existingNote.Tags = dto.Tags ?? new List<string>();
        existingNote.Folder = dto.Folder;
        existingNote.IsPinned = dto.IsPinned;
        existingNote.UpdatedAt = DateTime.UtcNow; 
    
        await _repository.UpdateAsync(existingNote);
    }
    
    public async Task DeleteNoteAsync(int id)
    {
        var existingNote = await _repository.GetByIdAsync(id);
        
        if (existingNote == null)
        {
            throw new KeyNotFoundException($"Note with id {id} not found");
        }
        
        await _repository.DeleteAsync(id);
    }
    
    public async Task<IEnumerable<string>> GetAvailableFoldersAsync()
    {
        return await _repository.GetDistinctFoldersAsync();
    }
    
    public async Task<IEnumerable<string>> GetAvailableTagsAsync()
    {
        return await _repository.GetDistinctTagsAsync();
    }
}