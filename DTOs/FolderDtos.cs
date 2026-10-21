using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.DTOs;

// Corpo de POST (criar) e PUT (renomear) em /api/folders
public class FolderRequestDto
{
    [Required(ErrorMessage = "Nome da pasta é obrigatório")]
    [MaxLength(100, ErrorMessage = "Nome da pasta deve ter no máximo 100 caracteres")]
    public string Name { get; set; } = string.Empty;
}

public class FolderResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int NoteCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
