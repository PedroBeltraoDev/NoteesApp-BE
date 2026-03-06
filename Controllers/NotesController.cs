using Microsoft.AspNetCore.Mvc;
using NotesApp.Api.DTOs;
using NotesApp.Api.Models;
using NotesApp.Api.Services;

namespace NotesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;
    
    public NotesController(INoteService noteService)
    {
        _noteService = noteService;
    }
    
    // GET: api/notes
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Note>>> GetNotes(
        [FromQuery] string? folder, 
        [FromQuery] string? tag)
    {
        var notes = await _noteService.GetFilteredNotesAsync(folder, tag);
        return Ok(notes);
    }
    
    // GET: api/notes/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Note>> GetNote(int id)
    {
        var note = await _noteService.GetNoteByIdAsync(id);
        return note == null ? NotFound() : Ok(note);
    }
    
    // GET: api/notes/search?query=titulo
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Note>>> SearchNotes([FromQuery] string query)
    {
        var notes = await _noteService.SearchNotesAsync(query);
        return Ok(notes);
    }
    
    // GET: api/notes/folders
    [HttpGet("folders")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableFolders()
    {
        var folders = await _noteService.GetAvailableFoldersAsync();
        return Ok(folders);
    }
    
    // GET: api/notes/tags
    [HttpGet("tags")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableTags()
    {
        var tags = await _noteService.GetAvailableTagsAsync();
        return Ok(tags);
    }
    
    // POST: api/notes
    [HttpPost]
    public async Task<ActionResult<Note>> CreateNote(CreateNoteDto dto)
    {
        var note = await _noteService.CreateNoteAsync(dto);
        return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
    }
    
    // PUT: api/notes/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNote(int id, UpdateNoteDto dto)
    {
        await _noteService.UpdateNoteAsync(id, dto);
        return NoContent();
    }
    
    // DELETE: api/notes/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNote(int id)
    {
        await _noteService.DeleteNoteAsync(id);
        return NoContent();
    }
}