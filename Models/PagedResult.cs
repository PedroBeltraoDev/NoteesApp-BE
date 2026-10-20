namespace NotesApp.Api.Models;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalItems);
