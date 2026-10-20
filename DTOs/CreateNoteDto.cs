using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.DTOs;

public class CreateNoteDto
{
    [Required(ErrorMessage = "Título é obrigatório")]
    [MinLength(3, ErrorMessage = "Título deve ter pelo menos 3 caracteres")]
    [MaxLength(200, ErrorMessage = "Título deve ter no máximo 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Conteúdo é obrigatório")]
    [MinLength(10, ErrorMessage = "Conteúdo deve ter pelo menos 10 caracteres")]
    [MaxLength(1000, ErrorMessage = "Conteúdo deve ter no máximo 1000 caracteres")]
    public string Content { get; set; } = string.Empty;
    
    [MaxLength(100, ErrorMessage = "Pasta deve ter no máximo 100 caracteres")]
    public string? Folder { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public bool IsPinned { get; set; }
}