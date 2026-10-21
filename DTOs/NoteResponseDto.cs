namespace NotesApp.Api.DTOs;

public class NoteResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? FolderId { get; set; }
    public string? Folder { get; set; }
    public IEnumerable<string> Tags { get; set; } = new List<string>();
    public bool IsPinned { get; set; }
}