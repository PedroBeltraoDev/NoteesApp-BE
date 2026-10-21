using NotesApp.Api.DTOs;
using NotesApp.Api.Mappers;
using NotesApp.Api.Models;
using Xunit;

namespace NotesApp.Api.Tests;

public class NoteMapperTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void ToEntity_CopiesFieldsAndSetsTimestamps()
    {
        var dto = new CreateNoteDto
        {
            Title = "Título",
            Content = "Conteúdo da nota",
            Folder = "Trabalho",
            Tags = ["a", "b"],
            IsPinned = true
        };

        var folder = new Folder { Id = 4, Name = "Trabalho" };
        var before = DateTime.UtcNow;
        var note = dto.ToEntity(OwnerId, folder);

        Assert.Equal(0, note.Id);
        Assert.Equal(OwnerId, note.UserId);
        Assert.Equal("Título", note.Title);
        Assert.Equal("Conteúdo da nota", note.Content);
        Assert.Equal(4, note.FolderId);
        Assert.Same(folder, note.Folder);
        Assert.Equal(["a", "b"], note.Tags);
        Assert.True(note.IsPinned);
        Assert.True(note.CreatedAt >= before);
        Assert.Equal(note.CreatedAt, note.UpdatedAt);
    }

    [Fact]
    public void ToEntity_CopiesTagList_InsteadOfSharingReference()
    {
        var dto = new CreateNoteDto { Tags = ["a"] };

        var note = dto.ToEntity(OwnerId);
        dto.Tags.Add("b");

        Assert.Equal(["a"], note.Tags);
    }

    [Fact]
    public void ApplyTo_UpdatesFieldsButKeepsIdAndCreatedAt()
    {
        var createdAt = DateTime.UtcNow.AddDays(-2);
        var note = new Note
        {
            Id = 7,
            Title = "Antigo",
            Content = "Conteúdo antigo",
            Tags = ["velha"],
            CreatedAt = createdAt
        };
        var dto = new UpdateNoteDto
        {
            Title = "Novo",
            Content = "Conteúdo novo",
            Folder = "Pasta",
            Tags = ["nova"],
            IsPinned = true
        };

        var folder = new Folder { Id = 9, Name = "Pasta" };

        dto.ApplyTo(note, folder);

        Assert.Equal(7, note.Id);
        Assert.Equal(createdAt, note.CreatedAt);
        Assert.Equal("Novo", note.Title);
        Assert.Equal("Conteúdo novo", note.Content);
        Assert.Equal(9, note.FolderId);
        Assert.Equal("Pasta", note.Folder!.Name);
        Assert.Equal(["nova"], note.Tags);
        Assert.True(note.IsPinned);
        Assert.NotNull(note.UpdatedAt);
        Assert.True(note.UpdatedAt > createdAt);
    }

    [Fact]
    public void ToResponse_CopiesAllFields()
    {
        var created = DateTime.UtcNow.AddHours(-1);
        var updated = DateTime.UtcNow;
        var note = new Note
        {
            Id = 3,
            Title = "T",
            Content = "C",
            FolderId = 2,
            Folder = new Folder { Id = 2, Name = "F" },
            Tags = ["x", "y"],
            IsPinned = true,
            CreatedAt = created,
            UpdatedAt = updated
        };

        var response = note.ToResponse();

        Assert.Equal(3, response.Id);
        Assert.Equal("T", response.Title);
        Assert.Equal("C", response.Content);
        Assert.Equal(2, response.FolderId);
        Assert.Equal("F", response.Folder);
        Assert.Equal(["x", "y"], response.Tags);
        Assert.True(response.IsPinned);
        Assert.Equal(created, response.CreatedAt);
        Assert.Equal(updated, response.UpdatedAt);
    }
}

public class NoteMapperFolderTests
{
    [Fact]
    public void ToEntity_WithoutFolder_LeavesTheNoteWithoutFolder()
    {
        var note = new CreateNoteDto { Title = "Título", Content = "" }.ToEntity(Guid.NewGuid());

        Assert.Null(note.FolderId);
        Assert.Null(note.Folder);
    }

    [Fact]
    public void ApplyTo_WithoutFolder_MovesTheNoteOutOfItsFolder()
    {
        var note = new Note { Id = 1, FolderId = 3, Folder = new Folder { Id = 3, Name = "Antiga" } };

        new UpdateNoteDto { Title = "Título", Content = "" }.ApplyTo(note, null);

        Assert.Null(note.FolderId);
        Assert.Null(note.Folder);
    }

    [Fact]
    public void ToResponse_WithoutFolder_HasNoFolderIdNorName()
    {
        var response = new Note { Id = 1, Title = "T" }.ToResponse();

        Assert.Null(response.FolderId);
        Assert.Null(response.Folder);
    }
}
