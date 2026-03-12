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
}

