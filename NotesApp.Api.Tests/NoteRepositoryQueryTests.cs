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
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, null, "Importante").ToQueryString();

        Assert.Contains("lower", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("importante", sql);
    }

    [Fact]
    public void BuildFilteredQuery_WithFolderAndTag_AppliesBothFilters()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, "Trabalho", "urgente").ToQueryString();

        Assert.Contains("Folders", sql);
        Assert.Contains("urgente", sql);
    }

    [Fact]
    public void BuildFilteredQuery_WithFolderId_FiltersByTheForeignKey()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, 7, null, null).ToQueryString();

        // O id vai como parâmetro da query (@folderId), nunca concatenado no SQL
        Assert.Contains("\"FolderId\" = @", sql);
        Assert.Contains("='7'", sql);
    }

    [Fact]
    public void BuildFilteredQuery_LoadsTheFolderSoTheResponseCanShowItsName()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, null, null).ToQueryString();

        Assert.Contains("LEFT JOIN \"Folders\"", sql);
    }

    [Fact]
    public void FolderSummaryQuery_CountsNotesInTheDatabaseAndKeepsTheOwnerFilter()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=u;Password=p")
            .Options;
        var repository = new FolderRepository(new AppDbContext(options));

        var sql = repository.BuildSummaryQuery(UserId).ToQueryString();

        Assert.Contains("COUNT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(UserId.ToString(), sql);
        Assert.Contains("lower", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFilteredQuery_PagedQuery_UsesStableOrderingAndLimitOffset()
    {
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, null, null).Skip(20).Take(10).ToQueryString();

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
        var sql = CreateRepository().BuildFilteredQuery(UserId, null, null, null).ToQueryString();

        Assert.Contains("\"UserId\" =", sql);
    }

    [Fact]
    public void BuildDistinctTagsQuery_AlwaysRestrictsToTheOwner()
    {
        var sql = CreateRepository().BuildDistinctTagsQuery(UserId).ToQueryString();

        Assert.Contains("\"UserId\" =", sql);
    }
}
