using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Infrastructure.BackgroundJobs;
using AutonomousResearchAgent.Infrastructure.External.SemanticScholar;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.Configure<SemanticScholarOptions>(configuration.GetSection(SemanticScholarOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector()));

        services.AddScoped<IPaperService, PaperService>();
        services.AddScoped<ISummaryService, SummaryService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddSingleton<IEmbeddingService, PlaceholderEmbeddingService>();
        services.AddSingleton<ISummarizationService, PlaceholderSummarizationService>();
        services.AddSingleton<IJobRunner, NoOpJobRunner>();

        services.AddHttpClient<ISemanticScholarClient, SemanticScholarClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SemanticScholarOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
