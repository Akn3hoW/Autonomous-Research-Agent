using AutonomousResearchAgent.Api.Authorization;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Api.Startup;

public sealed class JwtSigningKeyValidator : IStartupFilter
{
    private const string DefaultSigningKeyPlaceholder = "replace-this-development-key-with-a-secure-value";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var jwtOptions = app.ApplicationServices.GetRequiredService<IOptions<JwtOptions>>().Value;

            if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) ||
                jwtOptions.SigningKey == DefaultSigningKeyPlaceholder)
            {
                throw new InvalidOperationException(
                    "JWT signing key is not configured. Set a secure value for 'Jwt:SigningKey' in appsettings.json or environment variables. Do not use the default development placeholder in production.");
            }

            next(app);
        };
    }
}
