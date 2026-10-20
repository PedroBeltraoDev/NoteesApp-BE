using AutoMapper;
using NotesApp.Api.DTOs;
using NotesApp.Api.Models;
using NotesApp.Api.Repositories;

namespace NotesApp.Api.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _repository;
    private readonly ILogger<NoteService> _logger;
    private readonly IMapper _mapper;

    public NoteService(
        INoteRepository repository, 
        ILogger<NoteService> logger,
        IMapper mapper)
    {
        _repository = repository;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Note> GetByIdAsync(int id)
    {
        _logger.LogInformation("Buscando nota com ID {NoteId}", id);
        
        var note = await _repository.GetByIdAsync(id);
        
        if (note == null)
        {
            _logger.LogWarning("Nota com ID {NoteId} não encontrada", id);
            throw new KeyNotFoundException($"Nota com ID {id} não encontrada");
        }
        
        return note;
    }

    public async Task<IEnumerable<Note>> GetAllAsync()
    {
        _logger.LogInformation("Buscando todas as notas");
        return await _repository.GetAllAsync();
    }

    public async Task<IEnumerable<Note>> GetFilteredAsync(string? folder, string? tag)
    {
        _logger.LogInformation("Filtrando notas - Pasta: {Folder}, Tag: {Tag}", folder, tag);
        
        folder = folder?.Trim();
        tag = tag?.Trim();

        if (string.IsNullOrEmpty(folder) && string.IsNullOrEmpty(tag))
            return await GetAllAsync();

        return await _repository.GetFilteredAsync(folder, tag);
    }

    public async Task<Note> CreateAsync(CreateNoteDto dto)
    {
        _logger.LogInformation("Criando nova nota: {Title}", dto.Title);

        ValidateNoteData(dto.Title, dto.Content, dto.Folder, dto.Tags);
        dto.Folder = NormalizeFolder(dto.Folder);
        dto.Tags = NormalizeTags(dto.Tags);

        var note = _mapper.Map<Note>(dto);
    
        var created = await _repository.CreateAsync(note);
    
        _logger.LogInformation("Nota criada com sucesso - ID: {NoteId}", created.Id);
        return created;
    }

    public async Task<Note> UpdateAsync(int id, UpdateNoteDto dto)
    {
        _logger.LogInformation("Atualizando nota ID: {NoteId}", id);

        var existing = await GetByIdAsync(id);
        ValidateNoteData(dto.Title, dto.Content, dto.Folder, dto.Tags);
        dto.Folder = NormalizeFolder(dto.Folder);
        dto.Tags = NormalizeTags(dto.Tags);

        _mapper.Map(dto, existing);

        await _repository.UpdateAsync(existing);

        _logger.LogInformation("Nota atualizada com sucesso - ID: {NoteId}", id);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Excluindo nota ID: {NoteId}", id);
        
        await GetByIdAsync(id);
        await _repository.DeleteAsync(id);
        
        _logger.LogInformation("Nota excluída com sucesso - ID: {NoteId}", id);
    }

    public async Task<IEnumerable<string>> GetDistinctFoldersAsync()
    {
        return await _repository.GetDistinctFoldersAsync();
    }

    public async Task<IEnumerable<string>> GetDistinctTagsAsync()
    {
        return await _repository.GetDistinctTagsAsync();
    }
    
    private const int MaxFolderLength = 100;
    private const int MaxTags = 20;
    private const int MaxTagLength = 50;

    // Sem paramName no ArgumentException: o Message vai direto para o cliente
    // e o framework anexaria "(Parameter 'x')" ao texto.
    private static void ValidateNoteData(string title, string content, string? folder, List<string>? tags)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título é obrigatório");

        if (title.Length < 3 || title.Length > 200)
            throw new ArgumentException("Título deve ter entre 3 e 200 caracteres");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Conteúdo é obrigatório");

        if (content.Length < 10 || content.Length > 1000)
            throw new ArgumentException("Conteúdo deve ter entre 10 e 1000 caracteres");

        if (folder is { Length: > MaxFolderLength })
            throw new ArgumentException($"Pasta deve ter no máximo {MaxFolderLength} caracteres");

        if (tags is null)
            return;

        if (tags.Count > MaxTags)
            throw new ArgumentException($"Máximo de {MaxTags} tags por nota");

        if (tags.Any(t => t is { Length: > MaxTagLength }))
            throw new ArgumentException($"Cada tag deve ter no máximo {MaxTagLength} caracteres");
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