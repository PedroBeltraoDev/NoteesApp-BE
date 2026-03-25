using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using NotesApp.Api.DTOs;
using NotesApp.Api.Services;

namespace NotesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;
    private readonly ILogger<NotesController> _logger;
    private readonly IMapper _mapper;

    public NotesController(INoteService noteService, ILogger<NotesController> logger, IMapper mapper)
    {
        _noteService = noteService;
        _logger = logger;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<NoteResponseDto>>>> GetAll(
        [FromQuery] string? folder, 
        [FromQuery] string? tag)
    {
        try
        {
            var notes = await _noteService.GetFilteredAsync(folder, tag);
            var response = _mapper.Map<List<NoteResponseDto>>(notes);
            return Ok(ApiResponseDto<IEnumerable<NoteResponseDto>>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar notas");
            return StatusCode(500, ApiResponseDto<IEnumerable<NoteResponseDto>>.Fail("Erro interno ao buscar notas"));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> GetById(int id)
    {
        try
        {
            var note = await _noteService.GetByIdAsync(id);
            var response = _mapper.Map<NoteResponseDto>(note);
            return Ok(ApiResponseDto<NoteResponseDto>.Ok(response));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponseDto<NoteResponseDto>.Fail("Nota não encontrada"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar nota ID {NoteId}", id);
            return StatusCode(500, ApiResponseDto<NoteResponseDto>.Fail("Erro interno ao buscar nota"));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> Create([FromBody] CreateNoteDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponseDto<NoteResponseDto>.Fail("Dados inválidos"));
            
            //passa o DTO para o service
            var createdNote = await _noteService.CreateAsync(dto);
            
            var response = _mapper.Map<NoteResponseDto>(createdNote);
            
            return CreatedAtAction(
                nameof(GetById), 
                new { id = createdNote.Id }, 
                ApiResponseDto<NoteResponseDto>.Ok(response, "Nota criada com sucesso"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<NoteResponseDto>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar nota");
            return StatusCode(500, ApiResponseDto<NoteResponseDto>.Fail("Erro interno ao criar nota"));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponseDto<NoteResponseDto>>> Update(int id, [FromBody] UpdateNoteDto dto)
    {
        try
        {
            if (id != dto.Id)
                return BadRequest(ApiResponseDto<NoteResponseDto>.Fail("ID incompatível"));
            
            if (!ModelState.IsValid)
                return BadRequest(ApiResponseDto<NoteResponseDto>.Fail("Dados inválidos"));
            
            //passa o DTO para o service 
            var updatedNote = await _noteService.UpdateAsync(dto);
            
            var response = _mapper.Map<NoteResponseDto>(updatedNote);
            
            return Ok(ApiResponseDto<NoteResponseDto>.Ok(response, "Nota atualizada com sucesso"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponseDto<NoteResponseDto>.Fail("Nota não encontrada"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<NoteResponseDto>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar nota ID {NoteId}", id);
            return StatusCode(500, ApiResponseDto<NoteResponseDto>.Fail("Erro interno ao atualizar nota"));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponseDto<object>>> Delete(int id)
    {
        try
        {
            await _noteService.DeleteAsync(id);
            return Ok(ApiResponseDto<object>.Ok(null, "Nota excluída com sucesso"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponseDto<object>.Fail("Nota não encontrada"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir nota ID {NoteId}", id);
            return StatusCode(500, ApiResponseDto<object>.Fail("Erro interno ao excluir nota"));
        }
    }

    [HttpGet("folders")]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<string>>>> GetFolders()
    {
        try
        {
            var folders = await _noteService.GetDistinctFoldersAsync();
            return Ok(ApiResponseDto<IEnumerable<string>>.Ok(folders));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar pastas");
            return StatusCode(500, ApiResponseDto<IEnumerable<string>>.Fail("Erro ao buscar pastas"));
        }
    }

    [HttpGet("tags")]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<string>>>> GetTags()
    {
        try
        {
            var tags = await _noteService.GetDistinctTagsAsync();
            return Ok(ApiResponseDto<IEnumerable<string>>.Ok(tags));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar tags");
            return StatusCode(500, ApiResponseDto<IEnumerable<string>>.Fail("Erro ao buscar tags"));
        }
    }
}