using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotesApp.Api.DTOs;
using NotesApp.Api.Extensions;
using NotesApp.Api.Models;
using NotesApp.Api.Services;

namespace NotesApp.Api.Controllers;

// Pastas do usuário autenticado. Exceções (FolderNotFound, Conflict, Argument) viram HTTP no ExceptionHandlingMiddleware.
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IFolderService _folderService;

    public FoldersController(IFolderService folderService)
    {
        _folderService = folderService;
    }

    private static FolderResponseDto ToResponse(FolderSummary summary) => new()
    {
        Id = summary.Folder.Id,
        Name = summary.Folder.Name,
        NoteCount = summary.NoteCount,
        CreatedAt = summary.Folder.CreatedAt
    };

    /// <summary>Todas as pastas do usuário, inclusive as vazias, com o total de notas de cada uma.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<IEnumerable<FolderResponseDto>>>> GetAll()
    {
        var folders = await _folderService.GetAllAsync(User.GetUserId());
        return Ok(ApiResponseDto<IEnumerable<FolderResponseDto>>.Ok(folders.Select(ToResponse).ToList()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<FolderResponseDto>>> GetById(int id)
    {
        var folder = await _folderService.GetByIdAsync(User.GetUserId(), id);
        return Ok(ApiResponseDto<FolderResponseDto>.Ok(ToResponse(folder)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<FolderResponseDto>>> Create([FromBody] FolderRequestDto dto)
    {
        var created = await _folderService.CreateAsync(User.GetUserId(), dto.Name);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Folder.Id },
            ApiResponseDto<FolderResponseDto>.Ok(ToResponse(created), "Pasta criada com sucesso"));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<FolderResponseDto>>> Rename(int id, [FromBody] FolderRequestDto dto)
    {
        var renamed = await _folderService.RenameAsync(User.GetUserId(), id, dto.Name);
        return Ok(ApiResponseDto<FolderResponseDto>.Ok(ToResponse(renamed), "Pasta renomeada com sucesso"));
    }

    /// <summary>Exclui a pasta. As notas dela não são apagadas: ficam sem pasta.</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponseDto<object?>>> Delete(int id)
    {
        await _folderService.DeleteAsync(User.GetUserId(), id);
        return Ok(ApiResponseDto<object?>.Ok(null, "Pasta excluída com sucesso"));
    }
}
