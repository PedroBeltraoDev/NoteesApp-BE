using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Data;
using NotesApp.Api.Repositories;
using Xunit;

namespace NotesApp.Api.Tests;

public class NoteRepositoryQueryTests
{
    private static readonly Guid UserId = Guid.NewGuid();

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
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, "Importante").ToQueryString();

        Assert.Contains("lower", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("importante", sql);
    }

    [Fact]
    public void BuildFilteredQuery_WithFolderAndTag_AppliesBothFilters()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, "Trabalho", "urgente").ToQueryString();

        Assert.Contains("Folder", sql);
        Assert.Contains("urgente", sql);
    }

    [Fact]
    public void BuildFilteredQuery_PagedQuery_UsesStableOrderingAndLimitOffset()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, null).Skip(20).Take(10).ToQueryString();

        Assert.Contains("ORDER BY n.\"IsPinned\" DESC, n.\"CreatedAt\" DESC, n.\"Id\" DESC", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
    }

    [Fact]
    public void BuildDistinctTagsQuery_RunsInTheDatabase()
    {
        var sql = CreateRepository().BuildDistinctTagsQuery(UserId).ToQueryString();

        Assert.Contains("unnest", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFilteredQuery_AlwaysRestrictsToTheOwner()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, null).ToQueryString();

        Assert.Contains("\"UserId\" =", sql);
    }

    [Fact]
    public void BuildDistinctTagsQuery_AlwaysRestrictsToTheOwner()
    {
        var sql = CreateRepository().BuildDistinctTagsQuery(UserId).ToQueryString();

        Assert.Contains("\"UserId\" =", sql);
    }
}
