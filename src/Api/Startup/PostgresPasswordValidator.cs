namespace AutonomousResearchAgent.Api.Startup;

public sealed class PostgresPasswordValidator : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var connectionString = app.Configuration.GetConnectionString("Postgres") ?? string.Empty;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "PostgreSQL connection string is not configured. Set 'ConnectionStrings:Postgres' in appsettings.json or environment variables.");
            }

            if (connectionString.Contains("${POSTGRES_PASSWORD}", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Password=${POSTGRES_PASSWORD}", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PostgreSQL password is not configured. The placeholder '${POSTGRES_PASSWORD}' was detected in the connection string. Set the POSTGRES_PASSWORD environment variable.");
            }

            next(app);
        };
    }
}