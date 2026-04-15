using AutonomousResearchAgent.Application.Admin;
using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Auth;
using AutonomousResearchAgent.Application.Citations;
using AutonomousResearchAgent.Application.Chat;
using AutonomousResearchAgent.Application.Collections;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Duplicates;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.LiteratureReviews;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.ResearchGoals;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Application.Users;
using AutonomousResearchAgent.Application.Watchlist;
using AutonomousResearchAgent.Infrastructure.BackgroundJobs;
using AutonomousResearchAgent.Infrastructure.Configuration;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.External.SemanticScholar;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.Configure<SemanticScholarOptions>(configuration.GetSection(SemanticScholarOptions.SectionName));
        services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.SectionName));
        services.PostConfigure<OpenRouterOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            }
        });
        services.AddSingleton<IValidateOptions<OpenRouterOptions>, OpenRouterOptionsValidator>();
        services.AddSingleton<IValidateOptions<SemanticScholarOptions>, SemanticScholarOptionsValidator>();
        services.Configure<BackgroundJobOptions>(configuration.GetSection(BackgroundJobOptions.SectionName));
        services.Configure<DocumentProcessingOptions>(configuration.GetSection(DocumentProcessingOptions.SectionName));
        services.Configure<LocalEmbeddingOptions>(configuration.GetSection(LocalEmbeddingOptions.SectionName));
        services.Configure<SearchWeightsOptions>(configuration.GetSection(SearchWeightsOptions.SectionName));
        services.Configure<SummaryOptions>(configuration.GetSection(SummaryOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector()));

        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector()));

        services.AddScoped<IPaperService, PaperService>();
        services.AddScoped<IPaperDocumentService, PaperDocumentService>();
        services.AddScoped<PaperDocumentProcessingService>();
        services.AddScoped<ISummaryService, SummaryService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IJobRunner, AutonomousJobRunner>();
        services.AddScoped<IEmbeddingIndexingService, EmbeddingIndexingService>();
        services.AddScoped<IDocumentTextExtractor, LocalDocumentTextExtractor>();
        services.AddScoped<ITextChunkingService, RecursiveCharacterTextChunkingService>();
        services.AddScoped<ISummarizationService, OpenRouterSummarizationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICitationGraphService, CitationGraphService>();
        services.AddScoped<IResearchGoalService, ResearchGoalService>();
        services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
        services.Configure<DuplicateDetectionOptions>(configuration.GetSection(DuplicateDetectionOptions.SectionName));
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<ILiteratureReviewService, LiteratureReviewService>();
        services.AddScoped<ISavedSearchService, SavedSearchService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddHostedService<DatabaseJobWorker>();

        services.AddHttpClient<ISemanticScholarClient, SemanticScholarClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SemanticScholarOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHttpClient<OpenRouterChatClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenRouterOptions>>().Value;
            var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHttpClient<LocalEmbeddingHttpClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LocalEmbeddingOptions>>().Value;
            var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddTransient<ILocalEmbeddingClient>(serviceProvider => serviceProvider.GetRequiredService<LocalEmbeddingHttpClient>());
        services.AddTransient<IEmbeddingService>(serviceProvider => serviceProvider.GetRequiredService<LocalEmbeddingHttpClient>());

        var documentProcessingOptions = configuration.GetSection(DocumentProcessingOptions.SectionName).Get<DocumentProcessingOptions>() ?? new DocumentProcessingOptions();
        services.AddHttpClient("PaperDocuments", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(documentProcessingOptions.DownloadTimeoutSeconds);
        });

        services.AddHttpClient("Webhooks");

        return services;
    }
}
