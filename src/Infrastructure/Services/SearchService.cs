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

public enum VectorDistanceOperator
{
    CosineDistance,
    L2Distance,
    NegativeInnerProduct
}

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
            return await SearchWithFullTextAsync(dbContext, request, cancellationToken);
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

    private async Task<PagedResult<SearchResultModel>> SearchWithFullTextAsync(ApplicationDbContext dbContext, SearchRequestModel request, CancellationToken cancellationToken)
    {
        var query = request.Query.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PagedResult<SearchResultModel>([], request.PageNumber, request.PageSize, 0);
        }

        var tsquery = EscapeForTsQuery(query);
        var offset = (request.PageNumber - 1) * request.PageSize;

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT p.""Id"", p.""Title"", p.""Abstract"", p.""Authors"", p.""Year"", p.""Venue"", ts_rank(p.""SearchVector"", to_tsquery(@query)) AS ""Rank""
            FROM ""papers"" p
            WHERE p.""SearchVector"" @@ to_tsquery(@query)
            ORDER BY ""Rank"" DESC, p.""UpdatedAt"" DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var queryParam = new NpgsqlParameter("query", tsquery);
        var offsetParam = new NpgsqlParameter("offset", offset);
        var pageSizeParam = new NpgsqlParameter("pageSize", request.PageSize);
        command.Parameters.Add(queryParam);
        command.Parameters.Add(offsetParam);
        command.Parameters.Add(pageSizeParam);

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<SearchResultModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SearchResultModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<List<string>>(3).AsReadOnly(),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDouble(6),
                "keyword",
                new JsonObject()));
        }
        await reader.CloseAsync();

        using var countCommand = dbContext.Database.GetDbConnection().CreateCommand();
        countCommand.CommandText = $@"
            SELECT COUNT(*) FROM ""papers"" p
            WHERE p.""SearchVector"" @@ to_tsquery(@query)";
        countCommand.Parameters.Add(new NpgsqlParameter("query", tsquery));
        using var countReader = await countCommand.ExecuteReaderAsync(cancellationToken);
        await countReader.ReadAsync(cancellationToken);
        var totalCount = countReader.GetInt64(0);
        await countReader.CloseAsync();

        return new PagedResult<SearchResultModel>(items, request.PageNumber, request.PageSize, totalCount);
    }

    private static string EscapeForTsQuery(string value)
    {
        var escaped = value.Replace("'", "''").Replace("\\", "\\\\");
        var words = escaped.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" & ", words);
    }

    public async Task<PagedResult<SearchResultModel>> SemanticSearchAsync(SemanticSearchRequestModel request, CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync(request.Query, cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (IsPostgres(dbContext))
        {
            return await SemanticSearchWithDatabaseScoringAsync(dbContext, queryEmbedding, request, VectorDistanceOperator.CosineDistance, cancellationToken);
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

    private async Task<PagedResult<SearchResultModel>> SemanticSearchWithDatabaseScoringAsync(
        ApplicationDbContext dbContext,
        float[] queryEmbedding,
        SemanticSearchRequestModel request,
        VectorDistanceOperator distanceOperator,
        CancellationToken cancellationToken)
    {
        var operatorSymbol = distanceOperator switch
        {
            VectorDistanceOperator.CosineDistance => "<=>",
            VectorDistanceOperator.L2Distance => "<->",
            VectorDistanceOperator.NegativeInnerProduct => "<#>",
            _ => "<=>"
        };

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT pe.""Id"", pe.""Vector"" {operatorSymbol} @query_embedding AS ""Distance""
            FROM ""paper_embeddings"" pe
            WHERE pe.""PaperId"" IS NOT NULL
              AND pe.""Vector"" IS NOT NULL
              AND (pe.""EmbeddingType"" = 'PaperAbstract' OR pe.""EmbeddingType"" = 'PaperSummary')
            ORDER BY pe.""Vector"" {operatorSymbol} @query_embedding
            LIMIT @maxCandidates";

        var embeddingParam = new NpgsqlParameter("query_embedding", new Vector(queryEmbedding));
        command.Parameters.Add(embeddingParam);
        command.Parameters.Add(new NpgsqlParameter("maxCandidates", request.MaxCandidates));

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var embeddingIds = new List<(Guid Id, double Distance)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var distance = reader.GetDouble(1);
            embeddingIds.Add((reader.GetGuid(0), distance));
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

        var keywordRanked = keywordResults.Items
            .Select((item, index) => new { Item = item, Rank = index + 1 })
            .ToDictionary(x => x.Item.PaperId, x => x.Rank);

        var semanticRanked = semanticResults.Items
            .Select((item, index) => new { Item = item, Rank = index + 1 })
            .ToDictionary(x => x.Item.PaperId, x => x.Rank);

        var allPaperIds = keywordResults.Items
            .Concat(semanticResults.Items)
            .Select(x => x.PaperId)
            .Distinct()
            .ToList();

        var k = _searchWeights.RrfConstantK;

        var combined = allPaperIds
            .Select(paperId =>
            {
                var keywordItem = keywordResults.Items.FirstOrDefault(x => x.PaperId == paperId);
                var semanticItem = semanticResults.Items.FirstOrDefault(x => x.PaperId == paperId);
                var seed = keywordItem ?? semanticItem!;

                var keywordRank = keywordRanked.GetValueOrDefault(paperId, int.MaxValue);
                var semanticRank = semanticRanked.GetValueOrDefault(paperId, int.MaxValue);

                var keywordRrfScore = keywordRank != int.MaxValue ? 1.0 / (k + keywordRank) : 0;
                var semanticRrfScore = semanticRank != int.MaxValue ? 1.0 / (k + semanticRank) : 0;

                var rrfScore = keywordRrfScore + semanticRrfScore;

                var keywordScore = keywordItem?.Score ?? 0;
                var semanticScore = semanticItem?.Score ?? 0;

                return seed with
                {
                    MatchType = "hybrid",
                    Score = rrfScore,
                    Highlights = new JsonObject
                    {
                        ["keywordScore"] = keywordScore,
                        ["semanticScore"] = semanticScore,
                        ["keywordRrfScore"] = keywordRrfScore,
                        ["semanticRrfScore"] = semanticRrfScore,
                        ["keywordRank"] = keywordRank == int.MaxValue ? null : keywordRank,
                        ["semanticRank"] = semanticRank == int.MaxValue ? null : semanticRank
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

    public async Task<PagedResult<ChunkSearchResultModel>> SearchDocumentChunksAsync(ChunkSearchRequestModel request, CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync(request.Query, cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (IsPostgres(dbContext))
        {
            return await SearchChunksWithDatabaseScoringAsync(dbContext, queryEmbedding, request, cancellationToken);
        }

        return await SearchChunksInMemoryAsync(dbContext, queryEmbedding, request, cancellationToken);
    }

    private async Task<PagedResult<ChunkSearchResultModel>> SearchChunksWithDatabaseScoringAsync(
        ApplicationDbContext dbContext,
        float[] queryEmbedding,
        ChunkSearchRequestModel request,
        CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $@"
            SELECT pe.""DocumentChunkId"", pc.""Text"", p.""Title"", p.""Id"", pe.""Vector"" <@> @query_embedding AS ""Distance""
            FROM ""paper_embeddings"" pe
            INNER JOIN ""document_chunks"" pc ON pe.""DocumentChunkId"" = pc.""Id""
            INNER JOIN ""paper_documents"" pd ON pc.""PaperDocumentId"" = pd.""Id""
            INNER JOIN ""papers"" p ON pd.""PaperId"" = p.""Id""
            WHERE pe.""Vector"" IS NOT NULL
              AND pe.""EmbeddingType"" = 'DocumentChunk'
            ORDER BY pe.""Vector"" <@> @query_embedding
            LIMIT @maxCandidates";

        var embeddingParam = new NpgsqlParameter("query_embedding", new Vector(queryEmbedding));
        command.Parameters.Add(embeddingParam);
        command.Parameters.Add(new NpgsqlParameter("maxCandidates", request.MaxCandidates));

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ChunkSearchResultModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ChunkSearchResultModel(
                ChunkId: reader.GetGuid(0),
                PaperId: reader.GetGuid(3),
                PaperTitle: reader.GetString(2),
                ChunkText: reader.GetString(1),
                Score: reader.GetDouble(4)));
        }

        await reader.CloseAsync();

        if (results.Count == 0)
        {
            return new PagedResult<ChunkSearchResultModel>([], request.PageNumber, request.PageSize, 0);
        }

        var totalCount = results.Count;
        var pagedResults = results
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<ChunkSearchResultModel>(pagedResults, request.PageNumber, request.PageSize, totalCount);
    }

    private async Task<PagedResult<ChunkSearchResultModel>> SearchChunksInMemoryAsync(
        ApplicationDbContext dbContext,
        float[] queryEmbedding,
        ChunkSearchRequestModel request,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Include(e => e.DocumentChunk)
                .ThenInclude(c => c!.PaperDocument)
                    .ThenInclude(d => d.Paper)
            .Where(e =>
                e.Vector != null &&
                e.EmbeddingType == EmbeddingType.DocumentChunk &&
                e.DocumentChunk != null &&
                e.DocumentChunk.PaperDocument != null &&
                e.DocumentChunk.PaperDocument.Paper != null)
            .ToListAsync(cancellationToken);

        var scored = candidates
            .Select(e => new
            {
                Embedding = e,
                Score = VectorMath.CosineSimilarity(e.Vector!, queryEmbedding)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var results = scored
            .Select(x => new ChunkSearchResultModel(
                x.Embedding.DocumentChunk!.Id,
                x.Embedding.DocumentChunk.PaperDocument.Paper.Id,
                x.Embedding.DocumentChunk.PaperDocument.Paper.Title,
                x.Embedding.DocumentChunk.Text,
                x.Score))
            .ToList();

        var totalCount = results.Count;
        var pagedResults = results
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<ChunkSearchResultModel>(pagedResults, request.PageNumber, request.PageSize, totalCount);
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
