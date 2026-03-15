using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class SearchService(
    ApplicationDbContext dbContext,
    IEmbeddingService embeddingService,
    ILogger<SearchService> logger) : ISearchService
{
    public async Task<PagedResult<SearchResultModel>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken)
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

        var rankedQuery = filteredQuery
            .Select(p => new
            {
                Paper = p,
                MatchedInTitle = EF.Functions.ILike(p.Title, pattern),
                MatchedInAbstract = p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern),
                MatchedInSummary = p.Summaries.Any(s => s.SearchText != null && EF.Functions.ILike(s.SearchText, pattern)),
                MatchedInDocument = p.Documents.Any(d => d.ExtractedText != null && EF.Functions.ILike(d.ExtractedText, pattern)),
                Score =
                    (EF.Functions.ILike(p.Title, pattern) ? 1.0 : 0.0) +
                    ((p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern)) ? 0.6 : 0.0) +
                    (p.Summaries.Any(s => s.SearchText != null && EF.Functions.ILike(s.SearchText, pattern)) ? 0.4 : 0.0) +
                    (p.Documents.Any(d => d.ExtractedText != null && EF.Functions.ILike(d.ExtractedText, pattern)) ? 0.5 : 0.0)
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

    public async Task<PagedResult<SearchResultModel>> SemanticSearchAsync(SemanticSearchRequestModel request, CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync(request.Query, cancellationToken);

        var candidates = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Include(e => e.Paper)
            .Where(e => e.PaperId != null && e.Paper != null && e.Vector != null && e.EmbeddingType == EmbeddingType.PaperAbstract)
            .Take(request.MaxCandidates)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            logger.LogInformation("No embeddings available, falling back to keyword search for semantic query.");
            var fallback = await SearchAsync(new SearchRequestModel(request.Query, request.PageNumber, request.PageSize), cancellationToken);

            var mappedFallback = fallback.Items
                .Select(item => item with { MatchType = "semantic-fallback", Score = Math.Max(item.Score, 0.1) })
                .ToList();

            return new PagedResult<SearchResultModel>(mappedFallback, fallback.PageNumber, fallback.PageSize, fallback.TotalCount);
        }

        var ranked = candidates
            .Select(e => new
            {
                Embedding = e,
                Score = e.Vector is null ? 0d : VectorMath.CosineSimilarity(e.Vector, queryEmbedding)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var totalCount = ranked.Count;

        var limitedRanked = ranked
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var pageItems = limitedRanked
            .Select(x => new SearchResultModel(
                x.Embedding.Paper!.Id,
                x.Embedding.Paper.Title,
                x.Embedding.Paper.Abstract,
                x.Embedding.Paper.Authors.AsReadOnly(),
                x.Embedding.Paper.Year,
                x.Embedding.Paper.Venue,
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

    // TODO: These two searches are independent and could run concurrently via Task.WhenAll
    // once an IDbContextFactory is available (DbContext is not thread-safe).
    public async Task<PagedResult<SearchResultModel>> HybridSearchAsync(HybridSearchRequestModel request, CancellationToken cancellationToken)
    {
        var keywordResults = await SearchAsync(
            new SearchRequestModel(request.Query, 1, request.MaxCandidates),
            cancellationToken);

        var semanticResults = await SemanticSearchAsync(
            new SemanticSearchRequestModel(request.Query, 1, request.MaxCandidates, request.MaxCandidates),
            cancellationToken);

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
}
