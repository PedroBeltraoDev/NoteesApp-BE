namespace NotesApp.Api.DTOs;

public record PaginationDto(int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
