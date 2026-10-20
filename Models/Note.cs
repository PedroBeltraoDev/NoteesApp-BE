using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.Models;

public class Note
{
    public int Id { get; init; }
    
    public Guid UserId { get; init; }
    
    public User? User { get; set; }
    
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public List<string> Tags { get; set; } = [];
    
    public bool IsPinned { get; set; }
    
    [MaxLength(100)]
    public string? Folder { get; set; }
}