using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using NpgsqlTypes;
using Pgvector;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class SearchService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IEmbeddingService embeddingService,
    IOptions<SearchWeightsOptions> searchWeightsOptions,
    ILogger<SearchService> logger) : ISearchService
{
    private readonly SearchWeightsOptions _searchWeights = searchWeightsOptions.Value;
    public async Task<PagedResult<SearchResultModel>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await SearchWithContextAsync(dbContext, request, cancellationToken);
    }

    private async Task<PagedResult<SearchResultModel>> SearchWithContextAsync(ApplicationDbContext dbContext, SearchRequestModel request, CancellationToken cancellationToken)
    {
        if (IsPostgres(dbContext))
        {
            var pattern = QueryHelpers.ToILikePattern(request.Query);

            var filteredQuery = dbContext.Papers
                .AsNoTracking()
                .Where(p =>
                    EF.Functions.ILike(p.Title, pattern) ||
                    (p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern)) ||
                    p.Summaries.Any(s => s.SearchText != null && EF.Functions.ILike(s.SearchText, pattern)) ||
                    p.Documents.Any(d => d.ExtractedText != null && EF.Functions.ILike(d.ExtractedText, pattern)));

            var totalCount = await filteredQuery.LongCountAsync(cancellationToken);

            var titleWeight = _searchWeights.Title;
            var abstractWeight = _searchWeights.Abstract;
            var summaryWeight = _searchWeights.Summary;
            var documentWeight = _searchWeights.Document;

            var rankedQuery = filteredQuery
                .Select(p => new
                {
                    Paper = p,
                    MatchedInTitle = EF.Functions.ILike(p.Title, pattern),
                    MatchedInAbstract = p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern),
                    MatchedInSummary = p.Summaries.Any(s => s.SearchText != null && EF.Functions.ILike(s.SearchText, pattern)),
                    MatchedInDocument = p.Documents.Any(d => d.ExtractedText != null && EF.Functions.ILike(d.ExtractedText, pattern)),
                    Score =
                        (EF.Functions.ILike(p.Title, pattern) ? titleWeight : 0.0) +
                        ((p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern)) ? abstractWeight : 0.0) +
                        (p.Summaries.Any(s => s.SearchText != null && EF.Functions.ILike(s.SearchText, pattern)) ? summaryWeight : 0.0) +
                        (p.Documents.Any(d => d.ExtractedText != null && EF.Functions.ILike(d.ExtractedText, pattern)) ? documentWeight : 0.0)
                });

            var rankedPapers = await rankedQuery
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Paper.UpdatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var items = rankedPapers
                .Select(x => ToKeywordResult(x.Paper, request.Query, x.Score, x.MatchedInTitle, x.MatchedInAbstract, x.MatchedInSummary, x.MatchedInDocument))
                .ToList();

            return new PagedResult<SearchResultModel>(items, request.PageNumber, request.PageSize, totalCount);
        }

        var papers = await dbContext.Papers
            .AsNoTracking()
            .Include(p => p.Summaries)
            .Include(p => p.Documents)
            .ToListAsync(cancellationToken);

        var lowered = request.Query.Trim().ToLowerInvariant();
        var keywordMatches = papers
            .Select(p => new
            {
                Paper = p,
                MatchedInTitle = ContainsIgnoreCase(p.Title, lowered),
                MatchedInAbstract = ContainsIgnoreCase(p.Abstract, lowered),
                MatchedInSummary = p.Summaries.Any(s => ContainsIgnoreCase(s.SearchText, lowered)),
                MatchedInDocument = p.Documents.Any(d => ContainsIgnoreCase(d.ExtractedText, lowered))
            })
            .Where(x => x.MatchedInTitle || x.MatchedInAbstract || x.MatchedInSummary || x.MatchedInDocument)
            .Select(x => new
            {
                x.Paper,
                x.MatchedInTitle,
                x.MatchedInAbstract,
                x.MatchedInSummary,
                x.MatchedInDocument,
                Score = ComputeKeywordScore(x.MatchedInTitle, x.MatchedInAbstract, x.MatchedInSummary, x.MatchedInDocument)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Paper.UpdatedAt)
            .ToList();

        var pagedMatches = keywordMatches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var keywordItems = pagedMatches
            .Select(x => ToKeywordResult(x.Paper, request.Query, x.Score, x.MatchedInTitle, x.MatchedInAbstract, x.MatchedInSummary, x.MatchedInDocument))
            .ToList();

        return new PagedResult<SearchResultModel>(keywordItems, request.PageNumber, request.PageSize, keywordMatches.Count);
    }

    public async Task<PagedResult<SearchResultModel>> SemanticSearchAsync(SemanticSearchRequestModel request, CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync(request.Query, cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (IsPostgres(dbContext))
        {
            return await SemanticSearchWithDatabaseScoringAsync(dbContext, queryEmbedding, request, cancellationToken);
        }

        var candidates = await LoadSemanticCandidatesAsync(dbContext, cancellationToken);

        if (candidates.Count == 0)
        {
            logger.LogInformation("No embeddings available, falling back to keyword search for semantic query.");
            return await FallbackToKeywordSearchAsync(request, cancellationToken);
        }

        var ranked = candidates
            .Where(e => e.PaperId != null && e.Paper != null && e.Vector != null)
            .Select(e => new
            {
                Embedding = e,
                Score = e.Vector is null ? 0d : VectorMath.CosineSimilarity(e.Vector, queryEmbedding)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var aggregated = ranked
            .GroupBy(x => x.Embedding.PaperId ?? x.Embedding.Paper!.Id)
            .Select(group =>
            {
                var best = group.First();
                foreach (var candidate in group)
                {
                    if (candidate.Score > best.Score)
                    {
                        best = candidate;
                    }
                }

                return new
                {
                    Paper = best.Embedding.Paper!,
                    Embedding = best.Embedding,
                    Score = best.Score
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Paper.UpdatedAt)
            .ToList();

        var totalCount = aggregated.Count;

        var limitedRanked = aggregated
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var pageItems = limitedRanked
            .Select(x => new SearchResultModel(
                x.Paper.Id,
                x.Paper.Title,
                x.Paper.Abstract,
                x.Paper.Authors.AsReadOnly(),
                x.Paper.Year,
                x.Paper.Venue,
                x.Score,
                "semantic",
                new JsonObject
                {
                    ["modelName"] = x.Embedding.ModelName,
                    ["embeddingType"] = x.Embedding.EmbeddingType.ToString()
                }))
            .ToList();

        return new PagedResult<SearchResultModel>(pageItems, request.PageNumber, request.PageSize, totalCount);
    }

    private async Task<PagedResult<SearchResultModel>> SemanticSearchWithDatabaseScoringAsync(ApplicationDbContext dbContext, float[] queryEmbedding, SemanticSearchRequestModel request, CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT pe.""Id"", (pe.""Vector"" <-> @query_embedding) AS ""Distance""
            FROM ""paper_embeddings"" pe
            WHERE pe.""PaperId"" IS NOT NULL
              AND pe.""Vector"" IS NOT NULL
              AND (pe.""EmbeddingType"" = 'PaperAbstract' OR pe.""EmbeddingType"" = 'PaperSummary')
            ORDER BY pe.""Vector"" <-> @query_embedding
            LIMIT @maxCandidates";

        var embeddingParam = new NpgsqlParameter("query_embedding", new Vector(queryEmbedding));
        command.Parameters.Add(embeddingParam);
        command.Parameters.Add(new NpgsqlParameter("maxCandidates", request.MaxCandidates));

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var embeddingIds = new List<(Guid Id, double Distance)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            embeddingIds.Add((reader.GetGuid(0), 1 - reader.GetDouble(1)));
        }
        await reader.CloseAsync();

        if (embeddingIds.Count == 0)
        {
            logger.LogInformation("No embeddings available, falling back to keyword search for semantic query.");
            return await FallbackToKeywordSearchAsync(request, cancellationToken);
        }

        var ids = embeddingIds.Select(x => x.Id).ToList();
        var candidates = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Include(e => e.Paper)
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);

        var scoreMap = embeddingIds.ToDictionary(x => x.Id, x => x.Distance);

        var aggregated = candidates
            .GroupBy(x => x.PaperId ?? x.Paper!.Id)
            .Select(group =>
            {
                var bestEmbedding = group.OrderByDescending(e => scoreMap.GetValueOrDefault(e.Id, 0)).First();
                return new
                {
                    Paper = bestEmbedding.Paper!,
                    Embedding = bestEmbedding,
                    Score = scoreMap.GetValueOrDefault(bestEmbedding.Id, 0)
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Paper.UpdatedAt)
            .ToList();

        var totalCount = aggregated.Count;
        var items = aggregated
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new SearchResultModel(
                x.Paper.Id,
                x.Paper.Title,
                x.Paper.Abstract,
                x.Paper.Authors.AsReadOnly(),
                x.Paper.Year,
                x.Paper.Venue,
                x.Score,
                "semantic",
                new JsonObject
                {
                    ["modelName"] = x.Embedding.ModelName,
                    ["embeddingType"] = x.Embedding.EmbeddingType.ToString()
                }))
            .ToList();

        return new PagedResult<SearchResultModel>(items, request.PageNumber, request.PageSize, totalCount);
    }

    private async Task<PagedResult<SearchResultModel>> FallbackToKeywordSearchAsync(SemanticSearchRequestModel request, CancellationToken cancellationToken)
    {
        var fallback = await SearchAsync(new SearchRequestModel(request.Query, request.PageNumber, request.PageSize), cancellationToken);

        var mappedFallback = fallback.Items
            .Select(item => item with { MatchType = "semantic-fallback", Score = Math.Max(item.Score, 0.1) })
            .ToList();

        return new PagedResult<SearchResultModel>(mappedFallback, fallback.PageNumber, fallback.PageSize, fallback.TotalCount);
    }

    public async Task<PagedResult<SearchResultModel>> HybridSearchAsync(HybridSearchRequestModel request, CancellationToken cancellationToken)
    {
        var keywordTask = SearchAsync(
            new SearchRequestModel(request.Query, 1, request.MaxCandidates),
            cancellationToken);

        var semanticTask = SemanticSearchAsync(
            new SemanticSearchRequestModel(request.Query, 1, request.MaxCandidates, request.MaxCandidates),
            cancellationToken);

        await Task.WhenAll(keywordTask, semanticTask);

        var keywordResults = await keywordTask;
        var semanticResults = await semanticTask;

        var combined = keywordResults.Items
            .Concat(semanticResults.Items)
            .GroupBy(item => item.PaperId)
            .Select(group =>
            {
                var keywordScore = group.Where(g => g.MatchType == "keyword").Select(g => g.Score).DefaultIfEmpty(0d).Max();
                var semanticScore = group.Where(g => g.MatchType != "keyword").Select(g => g.Score).DefaultIfEmpty(0d).Max();
                var seed = group.First();

                return seed with
                {
                    MatchType = "hybrid",
                    Score = (keywordScore * request.KeywordWeight) + (semanticScore * request.SemanticWeight),
                    Highlights = new JsonObject
                    {
                        ["keywordScore"] = keywordScore,
                        ["semanticScore"] = semanticScore
                    }
                };
            })
            .OrderByDescending(item => item.Score)
            .ToList();

        var totalCount = combined.Count;
        var pageItems = combined
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<SearchResultModel>(pageItems, request.PageNumber, request.PageSize, totalCount);
    }

    private static SearchResultModel ToKeywordResult(
        Paper paper,
        string query,
        double score,
        bool matchedInTitle,
        bool matchedInAbstract,
        bool matchedInSummary,
        bool matchedInDocument)
    {
        var lowered = query.Trim().ToLowerInvariant();

        return new SearchResultModel(
            paper.Id,
            paper.Title,
            paper.Abstract,
            paper.Authors.AsReadOnly(),
            paper.Year,
            paper.Venue,
            score,
            "keyword",
            new JsonObject
            {
                ["query"] = lowered,
                ["matchedInTitle"] = matchedInTitle,
                ["matchedInAbstract"] = matchedInAbstract,
                ["matchedInSummary"] = matchedInSummary,
                ["matchedInDocument"] = matchedInDocument
            });
    }

    private async Task<List<PaperEmbedding>> LoadSemanticCandidatesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (IsPostgres(dbContext))
        {
            return await dbContext.PaperEmbeddings
                .AsNoTracking()
                .Include(e => e.Paper)
                .Include(e => e.Summary)
                .Where(e =>
                    e.PaperId != null &&
                    e.Paper != null &&
                    e.Vector != null &&
                    (e.EmbeddingType == EmbeddingType.PaperAbstract || e.EmbeddingType == EmbeddingType.PaperSummary || e.EmbeddingType == EmbeddingType.DocumentChunk))
                .ToListAsync(cancellationToken);
        }

        return await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Include(e => e.Paper)
            .Include(e => e.Summary)
            .Where(e =>
                e.PaperId != null &&
                e.Paper != null &&
                e.Vector != null &&
                (e.EmbeddingType == EmbeddingType.PaperAbstract || e.EmbeddingType == EmbeddingType.PaperSummary || e.EmbeddingType == EmbeddingType.DocumentChunk))
            .ToListAsync(cancellationToken);
    }

    private static bool ContainsIgnoreCase(string? value, string loweredNeedle)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(loweredNeedle))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant().Contains(loweredNeedle);
    }

    private double ComputeKeywordScore(bool matchedInTitle, bool matchedInAbstract, bool matchedInSummary, bool matchedInDocument) =>
        (matchedInTitle ? _searchWeights.Title : 0.0) +
        (matchedInAbstract ? _searchWeights.Abstract : 0.0) +
        (matchedInSummary ? _searchWeights.Summary : 0.0) +
        (matchedInDocument ? _searchWeights.Document : 0.0);

    private bool IsPostgres(DbContext dbContext) => string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);
}
