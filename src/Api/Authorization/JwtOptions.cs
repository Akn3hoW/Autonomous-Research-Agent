namespace AutonomousResearchAgent.Api.Authorization;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string? Issuer { get; set; }
    public string SigningKey { get; set; } = "replace-this-development-key-with-a-secure-value";
    public bool RequireHttpsMetadata { get; set; } = false;
}

