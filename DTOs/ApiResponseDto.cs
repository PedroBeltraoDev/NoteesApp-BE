namespace NotesApp.Api.DTOs;

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }

    public static ApiResponseDto<T> Ok(T data, string message = "Operação realizada com sucesso")
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponseDto<T> Fail(string message, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };
}
