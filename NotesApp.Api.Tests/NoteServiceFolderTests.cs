using Microsoft.Extensions.Logging;
using Moq;
using NotesApp.Api.DTOs;
using NotesApp.Api.Exceptions;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

// Como o NoteService decide a pasta de uma nota: por id (o jeito novo) ou só pelo nome (clientes antigos)
public class NoteServiceFolderTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<INoteRepository> _notes = new();
    private readonly Mock<IFolderRepository> _folders = new();
    private readonly NoteService _service;

    public NoteServiceFolderTests()
    {
        _service = new NoteService(_notes.Object, _folders.Object, new Mock<ILogger<NoteService>>().Object);
        _notes.Setup(r => r.CreateAsync(It.IsAny<Note>())).ReturnsAsync((Note n) => n);
    }

    [Fact]
    public async Task Create_WithAFolderId_UsesThatFolder()
    {
        var folder = new Folder { Id = 3, UserId = UserId, Name = "Trabalho" };
        _folders.Setup(f => f.GetByIdAsync(UserId, 3)).ReturnsAsync(folder);

        var note = await _service.CreateAsync(UserId, new CreateNoteDto { Title = "Nota", FolderId = 3 });

        Assert.Equal(3, note.FolderId);
        Assert.Same(folder, note.Folder);
    }

    [Fact]
    public async Task Create_WithAFolderThatIsNotTheCallers_ThrowsFolderNotFound_AndSavesNothing()
    {
        // GetByIdAsync é restrito ao dono: a pasta de outro usuário volta como inexistente
        _folders.Setup(f => f.GetByIdAsync(UserId, 3)).ReturnsAsync((Folder?)null);

        await Assert.ThrowsAsync<FolderNotFoundException>(() =>
            _service.CreateAsync(UserId, new CreateNoteDto { Title = "Nota", FolderId = 3 }));

        _notes.Verify(r => r.CreateAsync(It.IsAny<Note>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenBothIdAndNameAreSent_TheIdWinsAndTheNameIsIgnored()
    {
        var folder = new Folder { Id = 3, UserId = UserId, Name = "Escolhida" };
        _folders.Setup(f => f.GetByIdAsync(UserId, 3)).ReturnsAsync(folder);

        var note = await _service.CreateAsync(UserId, new CreateNoteDto { Title = "Nota", FolderId = 3, Folder = "Outra" });

        Assert.Equal(3, note.FolderId);
        _folders.Verify(f => f.GetByNameAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        _folders.Verify(f => f.CreateAsync(It.IsAny<Folder>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithOnlyAName_ReusesTheExistingFolder()
    {
        var folder = new Folder { Id = 5, UserId = UserId, Name = "Trabalho" };
        _folders.Setup(f => f.GetByNameAsync(UserId, "trabalho")).ReturnsAsync(folder);

        var note = await _service.CreateAsync(UserId, new CreateNoteDto { Title = "Nota", Folder = " trabalho " });

        Assert.Equal(5, note.FolderId);
        _folders.Verify(f => f.CreateAsync(It.IsAny<Folder>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithOnlyAnUnknownName_CreatesTheFolderForTheCaller()
    {
        Folder? created = null;
        _folders.Setup(f => f.CreateAsync(It.IsAny<Folder>()))
                .Callback<Folder>(f => created = f)
                .ReturnsAsync((Folder f) => f);

        var note = await _service.CreateAsync(UserId, new CreateNoteDto { Title = "Nota", Folder = "Nova" });

        Assert.NotNull(created);
        Assert.Equal("Nova", created.Name);
        Assert.Equal(UserId, created.UserId);
        Assert.Same(created, note.Folder);
    }

    [Fact]
    public async Task Update_WithoutAFolder_TakesTheNoteOutOfItsFolder()
    {
        var existing = new Note { Id = 1, UserId = UserId, Title = "Nota", FolderId = 3, Folder = new Folder { Id = 3, Name = "Antiga" } };
        _notes.Setup(r => r.GetByIdAsync(UserId, 1)).ReturnsAsync(existing);

        var updated = await _service.UpdateAsync(UserId, 1, new UpdateNoteDto { Title = "Nota", Content = "" });

        Assert.Null(updated.FolderId);
        Assert.Null(updated.Folder);
    }

    [Fact]
    public async Task Update_WithAFolderThatIsNotTheCallers_ThrowsFolderNotFound_AndSavesNothing()
    {
        var existing = new Note { Id = 1, UserId = UserId, Title = "Nota" };
        _notes.Setup(r => r.GetByIdAsync(UserId, 1)).ReturnsAsync(existing);
        _folders.Setup(f => f.GetByIdAsync(UserId, 9)).ReturnsAsync((Folder?)null);

        await Assert.ThrowsAsync<FolderNotFoundException>(() =>
            _service.UpdateAsync(UserId, 1, new UpdateNoteDto { Title = "Nota", Content = "", FolderId = 9 }));

        _notes.Verify(r => r.UpdateAsync(It.IsAny<Note>()), Times.Never);
    }

    [Fact]
    public async Task ListByFolderId_PassesTheIdToTheRepository()
    {
        _notes.Setup(r => r.GetFilteredAsync(UserId, 3, null, null)).ReturnsAsync(new List<Note>());

        await _service.GetFilteredAsync(UserId, 3, null, null);

        _notes.Verify(r => r.GetFilteredAsync(UserId, 3, null, null), Times.Once);
        _notes.Verify(r => r.GetAllAsync(It.IsAny<Guid>()), Times.Never);
    }
}
