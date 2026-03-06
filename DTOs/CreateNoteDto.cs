namespace NotesApp.Api.DTOs;

public class CreateNoteDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? Folder { get; set; }
}