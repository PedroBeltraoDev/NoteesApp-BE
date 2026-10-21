namespace NotesApp.Api.Exceptions;

// Herda de KeyNotFoundException (vira 404), mas o middleware responde "Pasta não encontrada" em vez de "Nota não encontrada"
public class FolderNotFoundException : KeyNotFoundException
{
    public FolderNotFoundException(int id) : base($"Pasta com ID {id} não encontrada") { }
}
