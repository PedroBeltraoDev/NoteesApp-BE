using System.ComponentModel.DataAnnotations;

namespace NotesApp.Api.Models;

// Uma pasta pertence a um usuário e agrupa notas. O nome é único por usuário, sem diferenciar maiúsculas
// de minúsculas (índice no banco). Excluir a pasta não apaga as notas: elas ficam sem pasta.
public class Folder
{
    public int Id { get; init; }

    public Guid UserId { get; init; }

    public User? User { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public List<Note> Notes { get; set; } = [];
}
