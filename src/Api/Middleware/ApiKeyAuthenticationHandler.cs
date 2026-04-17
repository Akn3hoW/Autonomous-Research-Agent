using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Api.Middleware;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly ApplicationDbContext _dbContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext dbContext) : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var keyHash = ComputeHash(providedApiKey);
        var userApiKey = await _dbContext.UserApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.ExpiresAt > DateTimeOffset.UtcNow);

        if (userApiKey == null)
        {
            return AuthenticateResult.Fail("Invalid or expired API key.");
        }

        userApiKey.LastUsedAt = DateTimeOffset.UtcNow;
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
            }
        });

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userApiKey.UserId.ToString()),
            new Claim(ClaimTypes.Name, userApiKey.User.Username),
            new Claim(ClaimTypes.Email, userApiKey.User.Email),
            new Claim("ApiKeyName", userApiKey.Name)
        };

        foreach (var role in userApiKey.User.UserRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Role.Name));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static string ComputeHash(string key)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }
}

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}