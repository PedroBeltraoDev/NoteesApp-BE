using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Data;
using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

public class FolderRepository : IFolderRepository
{
    private readonly AppDbContext _context;

    public FolderRepository(AppDbContext context)
    {
        _context = context;
    }

    // O filtro vem antes da projeção: o EF não traduz condições sobre o record já projetado
    internal IQueryable<FolderSummary> BuildSummaryQuery(Guid userId, int? id = null)
    {
        var folders = _context.Folders.Where(f => f.UserId == userId);

        if (id is not null)
            folders = folders.Where(f => f.Id == id);

        return folders
            .OrderBy(f => f.Name.ToLower())
            .ThenBy(f => f.Id)
            .Select(f => new FolderSummary(f, f.Notes.Count));
    }

    public async Task<IReadOnlyList<FolderSummary>> GetAllAsync(Guid userId)
    {
        return await BuildSummaryQuery(userId).AsNoTracking().ToListAsync();
    }

    public async Task<FolderSummary?> GetSummaryAsync(Guid userId, int id)
    {
        return await BuildSummaryQuery(userId, id).AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task<Folder?> GetByIdAsync(Guid userId, int id)
    {
        return await _context.Folders.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
    }

    public async Task<Folder?> GetByNameAsync(Guid userId, string name)
    {
        // Sem diferenciar maiúsculas: "Trabalho" e "trabalho" são a mesma pasta
        var lowerName = name.ToLower();
        return await _context.Folders.FirstOrDefaultAsync(f => f.UserId == userId && f.Name.ToLower() == lowerName);
    }

    public async Task<Folder> CreateAsync(Folder folder)
    {
        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();
        return folder;
    }

    public async Task UpdateAsync(Folder folder)
    {
        _context.Folders.Update(folder);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid userId, int id)
    {
        // Carrega as notas para que o EF as deixe sem pasta (no banco o FK também é ON DELETE SET NULL)
        var folder = await _context.Folders
            .Include(f => f.Notes)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (folder is null)
            return;

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();
    }
}
