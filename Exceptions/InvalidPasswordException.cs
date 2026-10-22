namespace NotesApp.Api.Exceptions;

// Senha de confirmação errada. Vira 400 (e não 401) com errors.code = "invalid_password":
// um 401 faria o front achar que a sessão caiu e deslogar a pessoa por um erro de digitação
public class InvalidPasswordException : Exception
{
    public const string Code = "invalid_password";

    public InvalidPasswordException() : base("Senha incorreta") { }
}
