using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NotesApp.Api.Exceptions;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;
using NotesApp.Api.Services;
using Xunit;

namespace NotesApp.Api.Tests;

public class FolderServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IFolderRepository> _repository = new();
    private readonly FolderService _service;

    public FolderServiceTests()
    {
        _service = new FolderService(_repository.Object, new Mock<ILogger<FolderService>>().Object);
    }

    [Theory]
    [InlineData("Trabalho", "Trabalho")]
    [InlineData("  Trabalho  ", "Trabalho")]
    [InlineData("Minha   pasta\tde  notas", "Minha pasta de notas")]
    public void NormalizeName_TrimsAndCollapsesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, FolderService.NormalizeName(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeName_WhenBlank_ThrowsArgumentException(string? input)
    {
        var exception = Assert.Throws<ArgumentException>(() => FolderService.NormalizeName(input));

        Assert.Equal("Nome da pasta é obrigatório", exception.Message);
    }

    [Fact]
    public void NormalizeName_WhenAboveTheLimit_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => FolderService.NormalizeName(new string('a', 101)));
        Assert.Equal(100, FolderService.NormalizeName(new string('a', 100)).Length);
    }

    [Fact]
    public async Task CreateAsync_SavesTheNormalizedNameForTheOwner()
    {
        Folder? saved = null;
        _repository.Setup(r => r.CreateAsync(It.IsAny<Folder>()))
                   .Callback<Folder>(f => saved = f)
                   .ReturnsAsync((Folder f) => f);

        var result = await _service.CreateAsync(UserId, "  Ideias  ");

        Assert.NotNull(saved);
        Assert.Equal("Ideias", saved.Name);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(0, result.NoteCount);
    }

    [Fact]
    public async Task CreateAsync_WhenTheNameExists_ThrowsConflictAndSavesNothing()
    {
        _repository.Setup(r => r.GetByNameAsync(UserId, "Ideias"))
                   .ReturnsAsync(new Folder { Id = 1, UserId = UserId, Name = "ideias" });

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(UserId, "Ideias"));

        _repository.Verify(r => r.CreateAsync(It.IsAny<Folder>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenTwoRequestsRace_TheUniqueIndexBecomesAConflict()
    {
        // 1ª consulta: ainda não existe; depois o índice único barra o INSERT e a pasta do vencedor aparece
        _repository.SetupSequence(r => r.GetByNameAsync(UserId, "Ideias"))
                   .ReturnsAsync((Folder?)null)
                   .ReturnsAsync(new Folder { Id = 2, UserId = UserId, Name = "Ideias" });
        _repository.Setup(r => r.CreateAsync(It.IsAny<Folder>())).ThrowsAsync(new DbUpdateException());

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(UserId, "Ideias"));
    }

    [Fact]
    public async Task CreateAsync_WhenTheDatabaseFailsForAnotherReason_TheErrorIsNotHidden()
    {
        _repository.Setup(r => r.CreateAsync(It.IsAny<Folder>())).ThrowsAsync(new DbUpdateException("falha"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.CreateAsync(UserId, "Ideias"));
    }

    [Fact]
    public async Task RenameAsync_ToTheSameFolderWithDifferentCase_IsAllowed()
    {
        var folder = new Folder { Id = 4, UserId = UserId, Name = "trabalho" };
        _repository.Setup(r => r.GetByIdAsync(UserId, 4)).ReturnsAsync(folder);
        _repository.Setup(r => r.GetByNameAsync(UserId, "Trabalho")).ReturnsAsync(folder); // ela mesma
        _repository.Setup(r => r.GetSummaryAsync(UserId, 4)).ReturnsAsync(new FolderSummary(folder, 3));

        var result = await _service.RenameAsync(UserId, 4, "Trabalho");

        Assert.Equal("Trabalho", folder.Name);
        Assert.Equal(3, result.NoteCount);
        _repository.Verify(r => r.UpdateAsync(folder), Times.Once);
    }

    [Fact]
    public async Task RenameAsync_ToAnotherFoldersName_ThrowsConflict()
    {
        var folder = new Folder { Id = 4, UserId = UserId, Name = "Pessoal" };
        _repository.Setup(r => r.GetByIdAsync(UserId, 4)).ReturnsAsync(folder);
        _repository.Setup(r => r.GetByNameAsync(UserId, "Trabalho"))
                   .ReturnsAsync(new Folder { Id = 9, UserId = UserId, Name = "Trabalho" });

        await Assert.ThrowsAsync<ConflictException>(() => _service.RenameAsync(UserId, 4, "Trabalho"));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Folder>()), Times.Never);
    }

    [Fact]
    public async Task RenameAsync_WhenTheFolderIsNotTheCallers_ThrowsFolderNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(UserId, 4)).ReturnsAsync((Folder?)null);

        await Assert.ThrowsAsync<FolderNotFoundException>(() => _service.RenameAsync(UserId, 4, "Qualquer"));
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsFolderNotFound()
    {
        _repository.Setup(r => r.GetSummaryAsync(UserId, 7)).ReturnsAsync((FolderSummary?)null);

        await Assert.ThrowsAsync<FolderNotFoundException>(() => _service.GetByIdAsync(UserId, 7));
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_ThrowsFolderNotFoundAndDeletesNothing()
    {
        _repository.Setup(r => r.GetByIdAsync(UserId, 7)).ReturnsAsync((Folder?)null);

        await Assert.ThrowsAsync<FolderNotFoundException>(() => _service.DeleteAsync(UserId, 7));

        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToTheRepositoryOfTheOwner()
    {
        _repository.Setup(r => r.GetByIdAsync(UserId, 7)).ReturnsAsync(new Folder { Id = 7, UserId = UserId });

        await _service.DeleteAsync(UserId, 7);

        _repository.Verify(r => r.DeleteAsync(UserId, 7), Times.Once);
    }
}
