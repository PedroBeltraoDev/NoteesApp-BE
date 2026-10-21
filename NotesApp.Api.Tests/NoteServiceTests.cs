using Microsoft.Extensions.Logging;
using Moq;
using NotesApp.Api.DTOs;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

public class NoteServiceTests
{
    private readonly Mock<INoteRepository> _repositoryMock;
    private readonly NoteService _noteService;
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public NoteServiceTests()
    {
        _repositoryMock = new Mock<INoteRepository>();
        var loggerMock = new Mock<ILogger<NoteService>>();
        
        _noteService = new NoteService(
            _repositoryMock.Object, 
            loggerMock.Object
        );
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenDtoIsValid_ReturnsCreatedNote()
    {
        // Arrange
        var createDto = new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres",
            Folder = "Testes",
            Tags = ["teste", "unitario"],
            IsPinned = false
        };

        var expectedNote = new Note
        {
            Id = 1,
            Title = createDto.Title,
            Content = createDto.Content,
            Folder = createDto.Folder,
            Tags = [.. createDto.Tags],
            IsPinned = createDto.IsPinned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Note>()))
                      .ReturnsAsync(expectedNote);

        // Act
        var result = await _noteService.CreateAsync(UserId, createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedNote.Id, result.Id);
        Assert.Equal(expectedNote.Title, result.Title);
        Assert.Equal(expectedNote.Content, result.Content);
        
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Note>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Conteúdo válido com mais de 10 caracteres")]
    [InlineData("   ", "Conteúdo válido com mais de 10 caracteres")]
    public async Task CreateAsync_WhenTitleIsMissing_ThrowsArgumentException(string title, string content)
    {
        // Arrange
        var createDto = new CreateNoteDto
        {
            Title = title,
            Content = content,
            Tags = []
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _noteService.CreateAsync(UserId, createDto));
    }

    // Não há tamanho mínimo: "Pagar luz" com anotação vazia ou "OK" como título são notas válidas
    [Theory]
    [InlineData("Título válido", "")]
    [InlineData("Título válido", "Curto")]
    [InlineData("OK", "Sim")]
    public async Task CreateAsync_WithShortOrEmptyContent_Succeeds(string title, string content)
    {
        // Arrange
        var createDto = new CreateNoteDto
        {
            Title = title,
            Content = content,
            Tags = []
        };

        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Note>()))
                      .ReturnsAsync((Note n) => n);

        // Act
        var result = await _noteService.CreateAsync(UserId, createDto);

        // Assert
        Assert.Equal(title, result.Title);
        Assert.Equal(content, result.Content);
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Note>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenTitleIsTooLong_ThrowsArgumentException()
    {
        var createDto = new CreateNoteDto { Title = new string('a', 201), Content = "Conteúdo" };

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(UserId, createDto));
    }

    [Fact]
    public async Task CreateAsync_WhenContentIsTooLong_ThrowsArgumentException()
    {
        var createDto = new CreateNoteDto { Title = "Título válido", Content = new string('a', 1001) };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(UserId, createDto));

        Assert.Equal("Conteúdo deve ter no máximo 1000 caracteres", exception.Message);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenNoteExists_ReturnsNote()
    {
        // Arrange
        var noteId = 1;
        var expectedNote = new Note
        {
            Id = noteId,
            Title = "Nota Existente",
            Content = "Conteúdo da nota",
            CreatedAt = DateTime.UtcNow,
            Tags = ["tag1", "tag2"]
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, noteId))
                      .ReturnsAsync(expectedNote);

        // Act
        var result = await _noteService.GetByIdAsync(UserId, noteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(noteId, result.Id);
        Assert.Equal(expectedNote.Title, result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoteNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var noteId = 999;
        
        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, noteId))
                      .ReturnsAsync((Note?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _noteService.GetByIdAsync(UserId, noteId));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenDtoIsValid_UpdatesNote()
    {
        // Arrange
        const int noteId = 1;
        var updateDto = new UpdateNoteDto
        {
            Title = "Título Atualizado",
            Content = "Conteúdo atualizado com mais de 10 caracteres",
            Folder = "Atualizados",
            Tags = ["atualizado"],
            IsPinned = true
        };

        var existingNote = new Note
        {
            Id = 1,
            Title = "Título Antigo",
            Content = "Conteúdo antigo",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Tags = ["antigo"]
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, noteId))
                      .ReturnsAsync(existingNote);

        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Note>()))
                      .Returns(Task.FromResult<Note?>(existingNote));

        // Act
        var result = await _noteService.UpdateAsync(UserId, noteId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(noteId, result.Id);
        Assert.Equal(updateDto.Title, result.Title);
        Assert.Equal(updateDto.Folder, result.Folder);
        Assert.Equal(["atualizado"], result.Tags);
        Assert.True(result.IsPinned);
        Assert.NotNull(result.UpdatedAt);
        
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Note>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNoteNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int noteId = 999;
        var updateDto = new UpdateNoteDto
        {
            Title = "Título Inválido",
            Content = "Conteúdo inválido",
            Tags = []
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, noteId))
                      .ReturnsAsync((Note?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _noteService.UpdateAsync(UserId, noteId, updateDto));
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WhenNoteExists_DeletesNote()
    {
        // Arrange
        var noteId = 1;
        
        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, noteId))
                      .ReturnsAsync(new Note { Id = noteId });

        _repositoryMock.Setup(r => r.DeleteAsync(UserId, noteId))
                      .Returns(Task.CompletedTask);

        // Act
        await _noteService.DeleteAsync(UserId, noteId);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(UserId, noteId), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNoteNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var noteId = 999;
        
        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, noteId))
                      .ReturnsAsync((Note?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _noteService.DeleteAsync(UserId, noteId));
    }

    #endregion

    #region GetFilteredAsync Tests

    [Fact]
    public async Task GetFilteredAsync_WhenNoFilters_ReturnsAllNotes()
    {
        // Arrange
        var allNotes = new List<Note>
        {
            new() { Id = 1, Title = "Nota 1", Content = "Conteúdo 1", Tags = [] },
            new() { Id = 2, Title = "Nota 2", Content = "Conteúdo 2", Tags = [] }
        };

        _repositoryMock.Setup(r => r.GetAllAsync(UserId))
                      .ReturnsAsync(allNotes);

        // Act
        var result = await _noteService.GetFilteredAsync(UserId, null!, null!);

        // Assert - Convert to list to avoid multiple enumeration
        var resultList = result.ToList();
        Assert.NotNull(resultList);
        Assert.Equal(2, resultList.Count);
    }

    [Fact]
    public async Task GetFilteredAsync_WhenFolderFilter_ReturnsNotesFromFolder()
    {
        // Arrange
        const string folder = "Testes";
        var notesInFolder = new List<Note>
        {
            new() { Id = 1, Title = "Nota 1", Content = "Conteúdo 1", Folder = folder, Tags = [] },
            new() { Id = 2, Title = "Nota 2", Content = "Conteúdo 2", Folder = folder, Tags = [] }
        };

        _repositoryMock.Setup(r => r.GetFilteredAsync(UserId, folder, null))
                      .ReturnsAsync(notesInFolder);

        // Act
        var result = await _noteService.GetFilteredAsync(UserId, folder, null!);

        // Assert - Convert to list to avoid multiple enumeration
        var resultList = result.ToList();
        Assert.NotNull(resultList);
        Assert.Equal(2, resultList.Count);
        Assert.All(resultList, n => Assert.Equal(folder, n.Folder));
    }

    #endregion

    #region GetDistinctFoldersAsync Tests

    [Fact]
    public async Task GetDistinctFoldersAsync_ReturnsUniqueFolders()
    {
        // Arrange
        var folders = new List<string> { "Pasta1", "Pasta2", "Pasta3" };

        _repositoryMock.Setup(r => r.GetDistinctFoldersAsync(UserId))
                      .ReturnsAsync(folders);

        // Act
        var result = await _noteService.GetDistinctFoldersAsync(UserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(folders, result);
    }

    #endregion

    #region GetDistinctTagsAsync Tests

    [Fact]
    public async Task GetDistinctTagsAsync_ReturnsUniqueTags()
    {
        // Arrange
        var tags = new List<string> { "tag1", "tag2", "tag3" };

        _repositoryMock.Setup(r => r.GetDistinctTagsAsync(UserId))
                      .ReturnsAsync(tags);

        // Act
        var result = await _noteService.GetDistinctTagsAsync(UserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tags, result);
    }

    #endregion

    #region Normalização e validação de pasta/tags

    [Fact]
    public async Task CreateAsync_NormalizesTagsAndFolder()
    {
        // Arrange
        var createDto = new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres",
            Folder = "  Trabalho  ",
            Tags = ["  Importante ", "importante", "", "   ", "Urgente"]
        };

        Note? saved = null;
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Note>()))
                      .Callback<Note>(n => saved = n)
                      .ReturnsAsync(new Note { Id = 1 });

        // Act
        await _noteService.CreateAsync(UserId, createDto);

        // Assert - a nota persistida já vem normalizada
        Assert.NotNull(saved);
        Assert.Equal("Trabalho", saved.Folder);
        Assert.Equal(["Importante", "Urgente"], saved.Tags);
    }

    [Fact]
    public async Task CreateAsync_WhenFolderIsBlank_StoresNull()
    {
        var createDto = new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres",
            Folder = "   "
        };

        Note? saved = null;
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Note>()))
                      .Callback<Note>(n => saved = n)
                      .ReturnsAsync(new Note { Id = 1 });

        await _noteService.CreateAsync(UserId, createDto);

        Assert.NotNull(saved);
        Assert.Null(saved.Folder);
    }

    [Fact]
    public async Task CreateAsync_WhenTooManyTags_ThrowsArgumentException()
    {
        var createDto = new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres",
            Tags = [.. Enumerable.Range(1, 21).Select(i => $"tag{i}")]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(UserId, createDto));
    }

    [Fact]
    public async Task CreateAsync_WhenTagIsTooLong_ThrowsArgumentException()
    {
        var createDto = new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres",
            Tags = [new string('a', 51)]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(UserId, createDto));
    }

    [Fact]
    public async Task CreateAsync_WhenFolderIsTooLong_ThrowsArgumentException()
    {
        var createDto = new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres",
            Folder = new string('a', 101)
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(UserId, createDto));
    }

    [Fact]
    public async Task CreateAsync_ValidationMessage_DoesNotLeakParameterName()
    {
        var createDto = new CreateNoteDto { Title = "", Content = "Conteúdo válido com mais de 10 caracteres" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(UserId, createDto));

        Assert.DoesNotContain("Parameter", ex.Message);
    }

    #endregion

    #region GetFilteredAsync - tag

    [Fact]
    public async Task GetFilteredAsync_WhenTagFilter_DelegatesToRepositoryWithTrimmedTag()
    {
        var notes = new List<Note> { new() { Id = 1, Tags = ["Importante"] } };

        _repositoryMock.Setup(r => r.GetFilteredAsync(UserId, null, "importante"))
                      .ReturnsAsync(notes);

        var result = await _noteService.GetFilteredAsync(UserId, null, "  importante ");

        Assert.Single(result);
        _repositoryMock.Verify(r => r.GetFilteredAsync(UserId, null, "importante"), Times.Once);
    }

    [Fact]
    public async Task GetFilteredAsync_WhenFolderAndTag_UsesSingleRepositoryQuery()
    {
        _repositoryMock.Setup(r => r.GetFilteredAsync(UserId, "Trabalho", "urgente"))
                      .ReturnsAsync(new List<Note>());

        await _noteService.GetFilteredAsync(UserId, "Trabalho", "urgente");

        _repositoryMock.Verify(r => r.GetFilteredAsync(UserId, "Trabalho", "urgente"), Times.Once);
        _repositoryMock.Verify(r => r.GetAllAsync(UserId), Times.Never);
    }

    #endregion

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_DelegatesToRepositoryWithTrimmedFilters()
    {
        var expected = new PagedResult<Note>([new Note { Id = 1 }], 42);
        _repositoryMock.Setup(r => r.GetPagedAsync(UserId, "Trabalho", "urgente", 2, 10))
                      .ReturnsAsync(expected);

        var result = await _noteService.GetPagedAsync(UserId, "  Trabalho ", " urgente  ", 2, 10);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetPagedAsync_WhenFiltersAreBlank_PassesNull()
    {
        _repositoryMock.Setup(r => r.GetPagedAsync(UserId, null, null, 1, 20))
                      .ReturnsAsync(new PagedResult<Note>([], 0));

        await _noteService.GetPagedAsync(UserId, "   ", "", 1, 20);

        _repositoryMock.Verify(r => r.GetPagedAsync(UserId, null, null, 1, 20), Times.Once);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetPagedAsync_WhenPageParametersAreInvalid_ThrowsArgumentException(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _noteService.GetPagedAsync(UserId, null, null, page, pageSize));

        _repositoryMock.Verify(
            r => r.GetPagedAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    #endregion

    #region Isolamento entre usuários

    [Fact]
    public async Task GetByIdAsync_WhenNoteBelongsToAnotherUser_ThrowsKeyNotFoundException()
    {
        // O repositório só devolve a nota para o dono: para outro usuário ela "não existe"
        _repositoryMock.Setup(r => r.GetByIdAsync(UserId, 5)).ReturnsAsync(new Note { Id = 5, UserId = UserId });
        _repositoryMock.Setup(r => r.GetByIdAsync(OtherUserId, 5)).ReturnsAsync((Note?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _noteService.GetByIdAsync(OtherUserId, 5));
        Assert.Equal(5, (await _noteService.GetByIdAsync(UserId, 5)).Id);
    }

    [Fact]
    public async Task UpdateAsync_WhenNoteBelongsToAnotherUser_ThrowsAndNeverSaves()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(OtherUserId, 5)).ReturnsAsync((Note?)null);

        var dto = new UpdateNoteDto { Title = "Título válido", Content = "Conteúdo válido com mais de 10 caracteres" };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _noteService.UpdateAsync(OtherUserId, 5, dto));
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Note>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenNoteBelongsToAnotherUser_ThrowsAndNeverDeletes()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(OtherUserId, 5)).ReturnsAsync((Note?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _noteService.DeleteAsync(OtherUserId, 5));
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SavesNoteOwnedByTheCaller()
    {
        Note? saved = null;
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Note>()))
                      .Callback<Note>(n => saved = n)
                      .ReturnsAsync(new Note { Id = 1 });

        await _noteService.CreateAsync(UserId, new CreateNoteDto
        {
            Title = "Nota de Teste",
            Content = "Conteúdo válido com mais de 10 caracteres"
        });

        Assert.NotNull(saved);
        Assert.Equal(UserId, saved.UserId);
    }

    #endregion
}
