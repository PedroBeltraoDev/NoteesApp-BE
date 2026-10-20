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
        var result = await _noteService.CreateAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedNote.Id, result.Id);
        Assert.Equal(expectedNote.Title, result.Title);
        Assert.Equal(expectedNote.Content, result.Content);
        
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Note>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Conteúdo válido com mais de 10 caracteres")]
    [InlineData("AB", "Conteúdo válido com mais de 10 caracteres")]
    public async Task CreateAsync_WhenTitleIsTooShort_ThrowsArgumentException(string title, string content)
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
            _noteService.CreateAsync(createDto));
    }

    [Theory]
    [InlineData("Título válido", "")]
    [InlineData("Título válido", "Curto")]
    public async Task CreateAsync_WhenContentIsTooShort_ThrowsArgumentException(string title, string content)
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
            _noteService.CreateAsync(createDto));
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

        _repositoryMock.Setup(r => r.GetByIdAsync(noteId))
                      .ReturnsAsync(expectedNote);

        // Act
        var result = await _noteService.GetByIdAsync(noteId);

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
        
        _repositoryMock.Setup(r => r.GetByIdAsync(noteId))
                      .ReturnsAsync((Note?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _noteService.GetByIdAsync(noteId));
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

        _repositoryMock.Setup(r => r.GetByIdAsync(noteId))
                      .ReturnsAsync(existingNote);

        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Note>()))
                      .Returns(Task.FromResult<Note?>(existingNote));

        // Act
        var result = await _noteService.UpdateAsync(noteId, updateDto);

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

        _repositoryMock.Setup(r => r.GetByIdAsync(noteId))
                      .ReturnsAsync((Note?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _noteService.UpdateAsync(noteId, updateDto));
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WhenNoteExists_DeletesNote()
    {
        // Arrange
        var noteId = 1;
        
        _repositoryMock.Setup(r => r.GetByIdAsync(noteId))
                      .ReturnsAsync(new Note { Id = noteId });

        _repositoryMock.Setup(r => r.DeleteAsync(noteId))
                      .Returns(Task.CompletedTask);

        // Act
        await _noteService.DeleteAsync(noteId);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(noteId), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNoteNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var noteId = 999;
        
        _repositoryMock.Setup(r => r.GetByIdAsync(noteId))
                      .ReturnsAsync((Note?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _noteService.DeleteAsync(noteId));
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

        _repositoryMock.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(allNotes);

        // Act
        var result = await _noteService.GetFilteredAsync(null!, null!);

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

        _repositoryMock.Setup(r => r.GetFilteredAsync(folder, null))
                      .ReturnsAsync(notesInFolder);

        // Act
        var result = await _noteService.GetFilteredAsync(folder, null!);

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

        _repositoryMock.Setup(r => r.GetDistinctFoldersAsync())
                      .ReturnsAsync(folders);

        // Act
        var result = await _noteService.GetDistinctFoldersAsync();

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

        _repositoryMock.Setup(r => r.GetDistinctTagsAsync())
                      .ReturnsAsync(tags);

        // Act
        var result = await _noteService.GetDistinctTagsAsync();

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
        await _noteService.CreateAsync(createDto);

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

        await _noteService.CreateAsync(createDto);

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

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(createDto));
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

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(createDto));
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

        await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(createDto));
    }

    [Fact]
    public async Task CreateAsync_ValidationMessage_DoesNotLeakParameterName()
    {
        var createDto = new CreateNoteDto { Title = "", Content = "Conteúdo válido com mais de 10 caracteres" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _noteService.CreateAsync(createDto));

        Assert.DoesNotContain("Parameter", ex.Message);
    }

    #endregion

    #region GetFilteredAsync - tag

    [Fact]
    public async Task GetFilteredAsync_WhenTagFilter_DelegatesToRepositoryWithTrimmedTag()
    {
        var notes = new List<Note> { new() { Id = 1, Tags = ["Importante"] } };

        _repositoryMock.Setup(r => r.GetFilteredAsync(null, "importante"))
                      .ReturnsAsync(notes);

        var result = await _noteService.GetFilteredAsync(null, "  importante ");

        Assert.Single(result);
        _repositoryMock.Verify(r => r.GetFilteredAsync(null, "importante"), Times.Once);
    }

    [Fact]
    public async Task GetFilteredAsync_WhenFolderAndTag_UsesSingleRepositoryQuery()
    {
        _repositoryMock.Setup(r => r.GetFilteredAsync("Trabalho", "urgente"))
                      .ReturnsAsync(new List<Note>());

        await _noteService.GetFilteredAsync("Trabalho", "urgente");

        _repositoryMock.Verify(r => r.GetFilteredAsync("Trabalho", "urgente"), Times.Once);
        _repositoryMock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    #endregion
}
