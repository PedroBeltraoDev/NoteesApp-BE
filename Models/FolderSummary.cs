namespace NotesApp.Api.Models;

// Pasta com o total de notas dentro dela
public record FolderSummary(Folder Folder, int NoteCount);
