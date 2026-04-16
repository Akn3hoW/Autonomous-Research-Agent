using System.Security.Claims;

namespace AutonomousResearchAgent.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetActorName(this ClaimsPrincipal principal)
    {
        return principal.Identity?.Name
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
    }

    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue("user_id")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(idClaim, out var id))
        {
            return id;
        }

        return null;
    }

    public static Guid? GetUserGuid(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue("user_id")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(idClaim, out var id))
        {
            return id;
        }

        return null;
    }
}

