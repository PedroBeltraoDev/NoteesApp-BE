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
    
    public async Task<IEnumerable<Note>> GetAllAsync(Guid userId)
    {
        return await _context.Notes
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }
    
    public async Task<Note?> GetByIdAsync(Guid userId, int id)
    {
        return await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    }
    
    internal IQueryable<Note> BuildFilteredQuery(Guid userId, string? folder, string? tag)
    {
        var query = _context.Notes.Where(n => n.UserId == userId);

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

    public async Task<IEnumerable<Note>> GetFilteredAsync(Guid userId, string? folder, string? tag)
    {
        return await BuildFilteredQuery(userId, folder, tag).ToListAsync();
    }
    
    public async Task<PagedResult<Note>> GetPagedAsync(Guid userId, string? folder, string? tag, int page, int pageSize)
    {
        var query = BuildFilteredQuery(userId, folder, tag);
        
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
    
    public async Task DeleteAsync(Guid userId, int id)
    {
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (note != null)
        {
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
        }
    }
    
    public async Task<IEnumerable<string>> GetDistinctFoldersAsync(Guid userId)
    {
        return await _context.Notes
            .Where(n => n.UserId == userId && !string.IsNullOrWhiteSpace(n.Folder))
            .Select(n => n.Folder!)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync();
    }
    
    internal IQueryable<string> BuildDistinctTagsQuery(Guid userId)
    {
        // unnest no banco; tags que só diferem por maiúsculas/minúsculas viram uma só
        // (mantém a grafia "menor" na ordenação, ex.: "Importante" antes de "importante")
        return _context.Notes
            .Where(n => n.UserId == userId)
            .SelectMany(n => n.Tags)
            .Where(t => t.Trim() != "")
            .GroupBy(t => t.Trim().ToLower())
            .Select(g => g.Min(t => t.Trim())!)
            .OrderBy(t => t.ToLower());
    }
    
    public async Task<IEnumerable<string>> GetDistinctTagsAsync(Guid userId)
    {
        return await BuildDistinctTagsQuery(userId).ToListAsync();
    }
}