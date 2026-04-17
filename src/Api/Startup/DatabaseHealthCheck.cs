using Microsoft.EntityFrameworkCore;
using AutonomousResearchAgent.Infrastructure.Persistence;

namespace AutonomousResearchAgent.Api.Startup;

public sealed class DatabaseHealthCheck : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                var canConnect = dbContext.Database.CanConnectAsync().GetAwaiter().GetResult();
                if (!canConnect)
                {
                    throw new InvalidOperationException("Unable to connect to the database. Please check your database connection string and ensure the database server is running.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to connect to the database: {ex.Message}", ex);
            }

            next(app);
        };
    }
}
