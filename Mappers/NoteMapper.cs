using NotesApp.Api.DTOs;
using NotesApp.Api.Models;

namespace NotesApp.Api.Mappers;

public static class NoteMapper
{
    public static Note ToEntity(this CreateNoteDto dto, Guid userId)
    {
        var now = DateTime.UtcNow;

        return new Note
        {
            UserId = userId,
            Title = dto.Title,
            Content = dto.Content,
            Folder = dto.Folder,
            Tags = [.. dto.Tags],
            IsPinned = dto.IsPinned,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static void ApplyTo(this UpdateNoteDto dto, Note note)
    {
        note.Title = dto.Title;
        note.Content = dto.Content;
        note.Folder = dto.Folder;
        note.Tags = [.. dto.Tags];
        note.IsPinned = dto.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;
    }

    public static NoteResponseDto ToResponse(this Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        Folder = note.Folder,
        Tags = [.. note.Tags],
        IsPinned = note.IsPinned
    };
}
