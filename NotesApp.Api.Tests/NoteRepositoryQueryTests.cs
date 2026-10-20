using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Data;
using NotesApp.Api.Repositories;
using Xunit;

namespace NotesApp.Api.Tests;

public class NoteRepositoryQueryTests
{
    // Não abre conexão: só garante que a query LINQ é traduzível pelo provider Npgsql.
    private static NoteRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=u;Password=p")
            .Options;

        return new NoteRepository(new AppDbContext(options));
    }

    [Fact]
    public void BuildFilteredQuery_WithTag_TranslatesToCaseInsensitiveSql()
    {
        var sql = CreateRepository().BuildFilteredQuery(null, "Importante").ToQueryString();

        Assert.Contains("lower", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("importante", sql);
    }

    [Fact]
    public void BuildFilteredQuery_WithFolderAndTag_AppliesBothFilters()
    {
        var sql = CreateRepository().BuildFilteredQuery("Trabalho", "urgente").ToQueryString();

        Assert.Contains("Folder", sql);
        Assert.Contains("urgente", sql);
    }
}
