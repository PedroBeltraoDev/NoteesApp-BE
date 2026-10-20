using System.Security.Claims;

namespace NotesApp.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id do usuário autenticado (claim "sub" do JWT).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sem identificação de usuário");
    }
}
