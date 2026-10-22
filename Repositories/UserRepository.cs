using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Data;
using NotesApp.Api.Models;

namespace NotesApp.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByEmailAsync(string normalizedEmail)
        => _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

    public Task<User?> GetByIdAsync(Guid id)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task AddAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task AddDemoAsync(User user, IEnumerable<Folder> folders, IEnumerable<Note> notes)
    {
        _context.Users.Add(user);
        _context.Folders.AddRange(folders); // as notas dentro das pastas entram junto
        _context.Notes.AddRange(notes);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountActiveDemosAsync(DateTime now)
        => _context.Users.CountAsync(u => u.IsDemo && u.DemoExpiresAt > now);

    public Task<int> DeleteExpiredDemosAsync(DateTime now)
        => DeleteWithEverythingAsync(_context.Users.Where(u => u.IsDemo && u.DemoExpiresAt <= now));

    public async Task DeleteAsync(Guid userId)
        => await DeleteWithEverythingAsync(_context.Users.Where(u => u.Id == userId));

    private async Task<int> DeleteWithEverythingAsync(IQueryable<User> users)
    {
        // No Postgres as chaves estrangeiras em cascata apagam notas, pastas e tokens junto com a conta
        if (_context.Database.IsRelational())
            return await users.ExecuteDeleteAsync();

        // Provedores sem cascata (banco em memória, nos testes): apaga o que é do dono "na mão"
        var ids = await users.Select(u => u.Id).ToListAsync();
        _context.Notes.RemoveRange(_context.Notes.Where(n => ids.Contains(n.UserId)));
        _context.Folders.RemoveRange(_context.Folders.Where(f => ids.Contains(f.UserId)));
        _context.RefreshTokens.RemoveRange(_context.RefreshTokens.Where(t => ids.Contains(t.UserId)));
        _context.Users.RemoveRange(_context.Users.Where(u => ids.Contains(u.Id)));
        await _context.SaveChangesAsync();
        return ids.Count;
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash)
        => _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task AddRefreshTokenAsync(RefreshToken token, DateTime now)
    {
        await RemoveExpiredAsync(token.UserId, now);
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
    }

    public async Task RotateRefreshTokenAsync(RefreshToken old, RefreshToken replacement, DateTime now)
    {
        old.RevokedAt = now;
        await RemoveExpiredAsync(old.UserId, now);
        _context.RefreshTokens.Add(replacement);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token, DateTime now)
    {
        token.RevokedAt ??= now;
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime now)
    {
        var active = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in active)
            token.RevokedAt = now;

        await _context.SaveChangesAsync();
    }

    // Tokens revogados mas ainda não expirados ficam: é com eles que se detecta reuso
    private async Task RemoveExpiredAsync(Guid userId, DateTime now)
    {
        var expired = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < now)
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(expired);
    }
}
