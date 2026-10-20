using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotesApp.Api.DTOs;
using NotesApp.Api.Extensions;
using NotesApp.Api.Mappers;
using NotesApp.Api.Services;

namespace NotesApp.Api.Controllers;

// Exceções (KeyNotFound, Argument, etc.) são traduzidas para HTTP pelo ExceptionHandlingMiddleware.
// Todas as ações exigem JWT e só enxergam as notas do usuário autenticado.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;

    public NotesController(INoteService noteService)
    {
        _noteService = noteService;
    }

    private const int DefaultPageSize = 20;

    /// <summary>
    /// Lista notas. Sem <c>page</c>/<c>pageSize</c> devolve todas (compatível com clientes antigos);
    /// com qualquer um dos dois devolve uma página e preenche <c>pagination</c> na resposta.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<NoteResponseDto>>>> GetAll(
        [FromQuery] string? folder,
        [FromQuery] string? tag,
        [FromQuery, Range(1, 100_000, ErrorMessage = "page deve estar entre 1 e 100000")] int? page,
        [FromQuery, Range(1, NoteService.MaxPageSize, ErrorMessage = "pageSize deve estar entre 1 e 100")] int? pageSize)
    {
        var userId = User.GetUserId();

        if (page is null && pageSize is null)
        {
            var notes = await _noteService.GetFilteredAsync(userId, folder, tag);
            var all = notes.Select(n => n.ToResponse()).ToList();
            return Ok(ApiResponseDto<IEnumerable<NoteResponseDto>>.Ok(all));
        }

        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? DefaultPageSize;

        var result = await _noteService.GetPagedAsync(userId, folder, tag, currentPage, currentPageSize);
        var items = result.Items.Select(n => n.ToResponse()).ToList();
        var pagination = new PaginationDto(currentPage, currentPageSize, result.TotalItems);

        return Ok(ApiResponseDto<IEnumerable<NoteResponseDto>>.Ok(items, pagination));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> GetById(int id)
    {
        var note = await _noteService.GetByIdAsync(User.GetUserId(), id);
        var response = note.ToResponse();
        return Ok(ApiResponseDto<NoteResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> Create([FromBody] CreateNoteDto dto)
    {
        var createdNote = await _noteService.CreateAsync(User.GetUserId(), dto);
        var response = createdNote.ToResponse();

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdNote.Id },
            ApiResponseDto<NoteResponseDto>.Ok(response, "Nota criada com sucesso"));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> Update(int id, [FromBody] UpdateNoteDto dto)
    {
        var updatedNote = await _noteService.UpdateAsync(User.GetUserId(), id, dto);
        var response = updatedNote.ToResponse();

        return Ok(ApiResponseDto<NoteResponseDto>.Ok(response, "Nota atualizada com sucesso"));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<object?>>> Delete(int id)
    {
        await _noteService.DeleteAsync(User.GetUserId(), id);
        return Ok(ApiResponseDto<object?>.Ok(null, "Nota excluída com sucesso"));
    }

    [HttpGet("folders")]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<string>>>> GetFolders()
    {
        var folders = await _noteService.GetDistinctFoldersAsync(User.GetUserId());
        return Ok(ApiResponseDto<IEnumerable<string>>.Ok(folders));
    }

    [HttpGet("tags")]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<string>>>> GetTags()
    {
        var tags = await _noteService.GetDistinctTagsAsync(User.GetUserId());
        return Ok(ApiResponseDto<IEnumerable<string>>.Ok(tags));
    }
}
