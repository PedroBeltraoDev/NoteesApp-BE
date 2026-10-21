using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.DTOs;

public class CreateNoteDto
{
    [Required(ErrorMessage = "Título é obrigatório")]
    [MaxLength(200, ErrorMessage = "Título deve ter no máximo 200 caracteres")]
    public string Title { get; set; } = string.Empty;
    
    // A anotação é opcional (uma nota pode ter só o título), mas o campo precisa vir: "" é válido, null não
    [Required(AllowEmptyStrings = true, ErrorMessage = "Conteúdo é obrigatório")]
    [MaxLength(1000, ErrorMessage = "Conteúdo deve ter no máximo 1000 caracteres")]
    public string Content { get; set; } = string.Empty;
    
    // A pasta é indicada pelo id. Clientes antigos mandam só o nome em Folder: a pasta de mesmo nome é usada
    // (ou criada). Se vierem os dois, vale o id.
    public int? FolderId { get; set; }

    [MaxLength(100, ErrorMessage = "Pasta deve ter no máximo 100 caracteres")]
    public string? Folder { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public bool IsPinned { get; set; }
}