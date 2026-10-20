using Microsoft.AspNetCore.Mvc;
using NotesApp.Api.DTOs;
using NotesApp.Api.Mappers;
using NotesApp.Api.Services;

namespace NotesApp.Api.Controllers;

// Exceções (KeyNotFound, Argument, etc.) são traduzidas para HTTP pelo ExceptionHandlingMiddleware.
[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;

    public NotesController(INoteService noteService)
    {
        _noteService = noteService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<NoteResponseDto>>>> GetAll(
        [FromQuery] string? folder,
        [FromQuery] string? tag)
    {
        var notes = await _noteService.GetFilteredAsync(folder, tag);
        var response = notes.Select(n => n.ToResponse()).ToList();
        return Ok(ApiResponseDto<IEnumerable<NoteResponseDto>>.Ok(response));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> GetById(int id)
    {
        var note = await _noteService.GetByIdAsync(id);
        var response = note.ToResponse();
        return Ok(ApiResponseDto<NoteResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> Create([FromBody] CreateNoteDto dto)
    {
        var createdNote = await _noteService.CreateAsync(dto);
        var response = createdNote.ToResponse();

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdNote.Id },
            ApiResponseDto<NoteResponseDto>.Ok(response, "Nota criada com sucesso"));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> Update(int id, [FromBody] UpdateNoteDto dto)
    {
        var updatedNote = await _noteService.UpdateAsync(id, dto);
        var response = updatedNote.ToResponse();

        return Ok(ApiResponseDto<NoteResponseDto>.Ok(response, "Nota atualizada com sucesso"));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<object?>>> Delete(int id)
    {
        await _noteService.DeleteAsync(id);
        return Ok(ApiResponseDto<object?>.Ok(null, "Nota excluída com sucesso"));
    }

    [HttpGet("folders")]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<string>>>> GetFolders()
    {
        var folders = await _noteService.GetDistinctFoldersAsync();
        return Ok(ApiResponseDto<IEnumerable<string>>.Ok(folders));
    }

    [HttpGet("tags")]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<string>>>> GetTags()
    {
        var tags = await _noteService.GetDistinctTagsAsync();
        return Ok(ApiResponseDto<IEnumerable<string>>.Ok(tags));
    }
}
