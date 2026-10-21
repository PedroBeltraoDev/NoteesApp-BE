using NotesApp.Api.DTOs;
using NotesApp.Api.Models;

namespace NotesApp.Api.Mappers;

public static class NoteMapper
{
    public static Note ToEntity(this CreateNoteDto dto, Guid userId, Folder? folder = null)
    {
        var now = DateTime.UtcNow;

        return new Note
        {
            UserId = userId,
            Title = dto.Title,
            Content = dto.Content,
            FolderId = folder?.Id,
            Folder = folder,
            Tags = [.. dto.Tags],
            IsPinned = dto.IsPinned,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static void ApplyTo(this UpdateNoteDto dto, Note note, Folder? folder = null)
    {
        note.Title = dto.Title;
        note.Content = dto.Content;
        note.FolderId = folder?.Id;
        note.Folder = folder;
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
        FolderId = note.FolderId,
        Folder = note.Folder?.Name,
        Tags = [.. note.Tags],
        IsPinned = note.IsPinned
    };
}
