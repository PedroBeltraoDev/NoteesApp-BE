using NotesApp.Api.DTOs;
using NotesApp.Api.Mappers;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;

namespace NotesApp.Api.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _repository;
    private readonly ILogger<NoteService> _logger;

    public NoteService(
        INoteRepository repository, 
        ILogger<NoteService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Note> GetByIdAsync(Guid userId, int id)
    {
        _logger.LogInformation("Buscando nota com ID {NoteId}", id);
        
        var note = await _repository.GetByIdAsync(userId, id);
        
        if (note == null)
        {
            _logger.LogWarning("Nota com ID {NoteId} não encontrada", id);
            throw new KeyNotFoundException($"Nota com ID {id} não encontrada");
        }
        
        return note;
    }

    public async Task<IEnumerable<Note>> GetAllAsync(Guid userId)
    {
        _logger.LogInformation("Buscando todas as notas");
        return await _repository.GetAllAsync(userId);
    }

    public async Task<IEnumerable<Note>> GetFilteredAsync(Guid userId, string? folder, string? tag)
    {
        _logger.LogInformation("Filtrando notas - Pasta: {Folder}, Tag: {Tag}", folder, tag);
        
        folder = NormalizeFilter(folder);
        tag = NormalizeFilter(tag);

        if (folder is null && tag is null)
            return await GetAllAsync(userId);

        return await _repository.GetFilteredAsync(userId, folder, tag);
    }

    public async Task<PagedResult<Note>> GetPagedAsync(Guid userId, string? folder, string? tag, int page, int pageSize)
    {
        _logger.LogInformation(
            "Listando notas paginadas - Pasta: {Folder}, Tag: {Tag}, Página: {Page}, Tamanho: {PageSize}",
            folder, tag, page, pageSize);
        
        if (page < 1)
            throw new ArgumentException("Página deve ser maior ou igual a 1");
        
        if (pageSize < 1 || pageSize > MaxPageSize)
            throw new ArgumentException($"Tamanho da página deve estar entre 1 e {MaxPageSize}");
        
        return await _repository.GetPagedAsync(userId, NormalizeFilter(folder), NormalizeFilter(tag), page, pageSize);
    }

    public async Task<Note> CreateAsync(Guid userId, CreateNoteDto dto)
    {
        _logger.LogInformation("Criando nova nota: {Title}", dto.Title);

        ValidateNoteData(dto.Title, dto.Content, dto.Folder, dto.Tags);
        dto.Folder = NormalizeFolder(dto.Folder);
        dto.Tags = NormalizeTags(dto.Tags);

        var note = dto.ToEntity(userId);
    
        var created = await _repository.CreateAsync(note);
    
        _logger.LogInformation("Nota criada com sucesso - ID: {NoteId}", created.Id);
        return created;
    }

    public async Task<Note> UpdateAsync(Guid userId, int id, UpdateNoteDto dto)
    {
        _logger.LogInformation("Atualizando nota ID: {NoteId}", id);

        var existing = await GetByIdAsync(userId, id);
        ValidateNoteData(dto.Title, dto.Content, dto.Folder, dto.Tags);
        dto.Folder = NormalizeFolder(dto.Folder);
        dto.Tags = NormalizeTags(dto.Tags);

        dto.ApplyTo(existing);

        await _repository.UpdateAsync(existing);

        _logger.LogInformation("Nota atualizada com sucesso - ID: {NoteId}", id);
        return existing;
    }

    public async Task DeleteAsync(Guid userId, int id)
    {
        _logger.LogInformation("Excluindo nota ID: {NoteId}", id);
        
        await GetByIdAsync(userId, id);
        await _repository.DeleteAsync(userId, id);
        
        _logger.LogInformation("Nota excluída com sucesso - ID: {NoteId}", id);
    }

    public async Task<IEnumerable<string>> GetDistinctFoldersAsync(Guid userId)
    {
        return await _repository.GetDistinctFoldersAsync(userId);
    }

    public async Task<IEnumerable<string>> GetDistinctTagsAsync(Guid userId)
    {
        return await _repository.GetDistinctTagsAsync(userId);
    }
    
    public const int MaxPageSize = 100;
    
    private const int MaxTitleLength = 200;
    private const int MaxContentLength = 1000;
    private const int MaxFolderLength = 100;
    private const int MaxTags = 20;
    private const int MaxTagLength = 50;

    // Sem paramName no ArgumentException: o Message vai direto para o cliente
    // e o framework anexaria "(Parameter 'x')" ao texto.
    private static void ValidateNoteData(string title, string content, string? folder, List<string>? tags)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título é obrigatório");

        if (title.Length > MaxTitleLength)
            throw new ArgumentException($"Título deve ter no máximo {MaxTitleLength} caracteres");

        // A anotação é opcional: uma nota pode ter só o título
        if (content is { Length: > MaxContentLength })
            throw new ArgumentException($"Conteúdo deve ter no máximo {MaxContentLength} caracteres");

        if (folder is { Length: > MaxFolderLength })
            throw new ArgumentException($"Pasta deve ter no máximo {MaxFolderLength} caracteres");

        if (tags is null)
            return;

        if (tags.Count > MaxTags)
            throw new ArgumentException($"Máximo de {MaxTags} tags por nota");

        if (tags.Any(t => t is { Length: > MaxTagLength }))
            throw new ArgumentException($"Cada tag deve ter no máximo {MaxTagLength} caracteres");
    }

    private static string? NormalizeFilter(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
    
    private static string? NormalizeFolder(string? folder)
    {
        var trimmed = folder?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    // Remove espaços, tags vazias e duplicatas (ignorando maiúsculas/minúsculas),
    // preservando a grafia da primeira ocorrência.
    private static List<string> NormalizeTags(List<string>? tags)
    {
        return (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .DistinctBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}