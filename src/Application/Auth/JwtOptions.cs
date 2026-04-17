namespace AutonomousResearchAgent.Application.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string? Issuer { get; set; }
    public string SigningKey { get; set; } = "replace-this-development-key-with-a-secure-value";
    public bool RequireHttpsMetadata { get; set; } = false;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
