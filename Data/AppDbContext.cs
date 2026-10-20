using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Models;

namespace NotesApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Note> Notes => Set<Note>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Folder).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();

            // Filtro por pasta
            entity.HasIndex(e => e.Folder);

            // Ordenação padrão da listagem (fixadas primeiro, mais recentes depois); Id desempata
            entity.HasIndex(e => new { e.IsPinned, e.CreatedAt, e.Id })
                .IsDescending(true, true, true);
        });
    }
}