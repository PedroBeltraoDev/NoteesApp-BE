using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Exceptions;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;

namespace NotesApp.Api.Services;

public class FolderService : IFolderService
{
    public const int MaxNameLength = 100;

    private const string DuplicateMessage = "Já existe uma pasta com esse nome";

    private readonly IFolderRepository _repository;
    private readonly ILogger<FolderService> _logger;

    public FolderService(IFolderRepository repository, ILogger<FolderService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FolderSummary>> GetAllAsync(Guid userId)
    {
        return await _repository.GetAllAsync(userId);
    }

    public async Task<FolderSummary> GetByIdAsync(Guid userId, int id)
    {
        return await _repository.GetSummaryAsync(userId, id) ?? throw new FolderNotFoundException(id);
    }

    public async Task<FolderSummary> CreateAsync(Guid userId, string name)
    {
        var normalized = NormalizeName(name);

        if (await _repository.GetByNameAsync(userId, normalized) is not null)
            throw new ConflictException(DuplicateMessage);

        var folder = new Folder { UserId = userId, Name = normalized };

        try
        {
            await _repository.CreateAsync(folder);
        }
        catch (DbUpdateException)
        {
            // Duas criações simultâneas com o mesmo nome: o índice único barrou a segunda
            if (await _repository.GetByNameAsync(userId, normalized) is not null)
                throw new ConflictException(DuplicateMessage);

            throw;
        }

        _logger.LogInformation("Pasta criada - ID: {FolderId}", folder.Id);
        return new FolderSummary(folder, 0);
    }

    public async Task<FolderSummary> RenameAsync(Guid userId, int id, string name)
    {
        var normalized = NormalizeName(name);

        var folder = await _repository.GetByIdAsync(userId, id) ?? throw new FolderNotFoundException(id);

        // Trocar só maiúsculas/minúsculas da própria pasta ("trabalho" -> "Trabalho") é permitido
        var sameName = await _repository.GetByNameAsync(userId, normalized);
        if (sameName is not null && sameName.Id != folder.Id)
            throw new ConflictException(DuplicateMessage);

        folder.Name = normalized;

        try
        {
            await _repository.UpdateAsync(folder);
        }
        catch (DbUpdateException)
        {
            var winner = await _repository.GetByNameAsync(userId, normalized);
            if (winner is not null && winner.Id != folder.Id)
                throw new ConflictException(DuplicateMessage);

            throw;
        }

        _logger.LogInformation("Pasta renomeada - ID: {FolderId}", id);
        return await GetByIdAsync(userId, id);
    }

    public async Task DeleteAsync(Guid userId, int id)
    {
        if (await _repository.GetByIdAsync(userId, id) is null)
            throw new FolderNotFoundException(id);

        await _repository.DeleteAsync(userId, id);
        _logger.LogInformation("Pasta excluída - ID: {FolderId}", id);
    }

    // Aparar e juntar espaços repetidos; sem paramName no ArgumentException (a mensagem vai direto ao cliente)
    internal static string NormalizeName(string? name)
    {
        var normalized = string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length == 0)
            throw new ArgumentException("Nome da pasta é obrigatório");

        if (normalized.Length > MaxNameLength)
            throw new ArgumentException($"Nome da pasta deve ter no máximo {MaxNameLength} caracteres");

        return normalized;
    }
}
