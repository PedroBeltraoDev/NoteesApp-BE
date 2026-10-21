namespace NotesApp.Api.Exceptions;

// A conta demo passou do prazo. Vira 401 com errors.code = "demo_expired", para o front mostrar a tela de tempo esgotado
public class DemoExpiredException : UnauthorizedAccessException
{
    public const string Code = "demo_expired";

    public DemoExpiredException() : base("O tempo da conta demo acabou") { }
}

// Há demos demais ao mesmo tempo (vira 503)
public class DemoUnavailableException : Exception
{
    public DemoUnavailableException() : base("Há muitas contas demo ativas agora. Tente novamente em alguns minutos") { }
}
