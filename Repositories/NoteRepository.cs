using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Data;
using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly AppDbContext _context;
    
    public NoteRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<IEnumerable<Note>> GetAllAsync()
    {
        return await _context.Notes
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Note?> GetByIdAsync(int id)
    {
        return await _context.Notes.FindAsync(id);
    }
    
    public async Task<IEnumerable<Note>> GetByFolderAsync(string folder)
    {
        return await _context.Notes
            .Where(n => n.Folder == folder)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Note>> GetByTagAsync(string tag)
    {
        return await _context.Notes
            .Where(n => n.Tags.Contains(tag))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Note>> GetFilteredAsync(string? folder, string? tag)
    {
        var query = _context.Notes.AsQueryable();
        
        if (!string.IsNullOrEmpty(folder))
        {
            query = query.Where(n => n.Folder == folder);
        }
        
        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(n => n.Tags.Contains(tag));
        }
        
        return await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Note> CreateAsync(Note note)
    {
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();
        return note;
    }
    
    public async Task UpdateAsync(Note note)
    {
        _context.Notes.Update(note);
        await _context.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note != null)
        {
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
        }
    }
    
    public async Task<IEnumerable<string>> GetDistinctFoldersAsync()
    {
        var allFolders = new[] { "Projects", "Personal", "Learning", "Travel", "Archives" };
        var dbFolders = await _context.Notes
            .Where(n => n.Folder != null)
            .Select(n => n.Folder)
            .Distinct()
            .ToListAsync();
        
        return allFolders.Concat(dbFolders ?? Enumerable.Empty<string>()).Distinct();
    }
    
    public async Task<IEnumerable<string>> GetDistinctTagsAsync()
    {
        var defaultTags = new[] { "#Work", "#Design", "#Idea" };
        var allNotes = await _context.Notes.ToListAsync();
        var dbTags = allNotes.SelectMany(n => n.Tags).Distinct();
        
        return defaultTags.Concat(dbTags).Distinct();
    }
}