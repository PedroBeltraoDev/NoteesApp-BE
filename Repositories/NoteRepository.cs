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
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }
    
    public async Task<Note?> GetByIdAsync(int id)
    {
        return await _context.Notes.FindAsync(id);
    }
    
    internal IQueryable<Note> BuildFilteredQuery(string? folder, string? tag)
    {
        var query = _context.Notes.AsQueryable();

        if (!string.IsNullOrEmpty(folder))
        {
            query = query.Where(n => n.Folder == folder);
        }

        if (!string.IsNullOrEmpty(tag))
        {
            // Case-insensitive: "Importante" e "importante" são a mesma tag
            var lowerTag = tag.ToLower();
            query = query.Where(n => n.Tags.Any(t => t.ToLower() == lowerTag));
        }

        // Id como desempate mantém a ordem estável entre páginas
        return query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id);
    }

    public async Task<IEnumerable<Note>> GetFilteredAsync(string? folder, string? tag)
    {
        return await BuildFilteredQuery(folder, tag).ToListAsync();
    }
    
    public async Task<PagedResult<Note>> GetPagedAsync(string? folder, string? tag, int page, int pageSize)
    {
        var query = BuildFilteredQuery(folder, tag);
        
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return new PagedResult<Note>(items, total);
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
        return await _context.Notes
            .Where(n => !string.IsNullOrWhiteSpace(n.Folder))
            .Select(n => n.Folder!)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync();
    }
    
    internal IQueryable<string> BuildDistinctTagsQuery()
    {
        // unnest no banco; tags que só diferem por maiúsculas/minúsculas viram uma só
        // (mantém a grafia "menor" na ordenação, ex.: "Importante" antes de "importante")
        return _context.Notes
            .SelectMany(n => n.Tags)
            .Where(t => t.Trim() != "")
            .GroupBy(t => t.Trim().ToLower())
            .Select(g => g.Min(t => t.Trim())!)
            .OrderBy(t => t.ToLower());
    }
    
    public async Task<IEnumerable<string>> GetDistinctTagsAsync()
    {
        return await BuildDistinctTagsQuery().ToListAsync();
    }
}