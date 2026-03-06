namespace NotesApp.Api.Models;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsPinned { get; set; } = false;
    public string? Folder { get; set; }
}