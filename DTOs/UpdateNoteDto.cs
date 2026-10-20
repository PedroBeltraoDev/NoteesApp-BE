using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.DTOs;

public class UpdateNoteDto
{
    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MinLength(10)]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Folder { get; set; }

    public List<string> Tags { get; set; } = [];

    public bool IsPinned { get; set; }
}
