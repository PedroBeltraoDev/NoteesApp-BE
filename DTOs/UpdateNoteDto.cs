using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.DTOs;

public class UpdateNoteDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Folder { get; set; }

    public List<string> Tags { get; set; } = [];

    public bool IsPinned { get; set; }
}
