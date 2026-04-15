using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Recommendations;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class RecommendationService(ApplicationDbContext dbContext) : IRecommendationService
{
    public async Task<PagedResult<PaperRecommendationModel>> GetRecommendationsAsync(RecommendationQuery query, CancellationToken cancellationToken = default)
    {
        var readPaperIds = await dbContext.PaperReadingSessions
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId && x.Status == ReadingStatus.Read)
            .Select(x => x.PaperId)
            .ToListAsync(cancellationToken);

        if (readPaperIds.Count == 0)
        {
            return new PagedResult<PaperRecommendationModel>(
                [],
                query.PageNumber,
                query.PageSize,
                0);
        }

        var readEmbeddings = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Where(x => readPaperIds.Contains(x.PaperId ?? Guid.Empty) && x.EmbeddingType == EmbeddingType.PaperAbstract && x.Vector != null)
            .Select(x => x.Vector)
            .ToListAsync(cancellationToken);

        var readVectors = readEmbeddings.Where(v => v != null).Select(v => v!.ToArray()).ToList();

        if (readVectors.Count == 0)
        {
            return new PagedResult<PaperRecommendationModel>(
                [],
                query.PageNumber,
                query.PageSize,
                0);
        }

        var averageReadVector = AverageVectors(readVectors);

        var unreadPaperIds = await dbContext.PaperReadingSessions
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId)
            .Select(x => x.PaperId)
            .ToListAsync(cancellationToken);

        var candidatePapers = await dbContext.Papers
            .AsNoTracking()
            .Where(p => !unreadPaperIds.Contains(p.Id) && p.Status == PaperStatus.Ready)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Authors,
                p.Year,
                p.Venue,
                p.CitationCount,
                p.Status,
                p.CreatedAt,
                p.Embeddings
            })
            .ToListAsync(cancellationToken);

        var scoredPapers = new List<(Guid Id, string Title, List<string> Authors, int? Year, string? Venue, int CitationCount, string Status, DateTimeOffset CreatedAt, double Score)>();

        foreach (var paper in candidatePapers)
        {
            var embedding = paper.Embeddings
                .FirstOrDefault(e => e.EmbeddingType == EmbeddingType.PaperAbstract && e.Vector != null);

            if (embedding?.Vector == null) continue;

            var similarity = VectorMath.CosineSimilarity(averageReadVector, embedding.Vector.ToArray());
            scoredPapers.Add((paper.Id, paper.Title, paper.Authors, paper.Year, paper.Venue, paper.CitationCount, paper.Status.ToString(), paper.CreatedAt, similarity));
        }

        var topPapers = scoredPapers
            .OrderByDescending(x => x.Score)
            .Take(query.MaxRecommendations)
            .ToList();

        var totalCount = topPapers.Count;

        var pagedPapers = topPapers
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new PaperRecommendationModel(
                x.Id,
                x.Title,
                x.Authors,
                x.Year,
                x.Venue,
                x.CitationCount,
                x.Status,
                x.Score,
                x.CreatedAt))
            .ToList();

        return new PagedResult<PaperRecommendationModel>(pagedPapers, query.PageNumber, query.PageSize, totalCount);
    }

    private static float[] AverageVectors(List<float[]> vectors)
    {
        if (vectors.Count == 0) return [];

        var dimension = vectors[0].Length;
        var result = new float[dimension];

        foreach (var vector in vectors)
        {
            for (var i = 0; i < dimension; i++)
            {
                result[i] += vector[i];
            }
        }

        var count = (float)vectors.Count;
        for (var i = 0; i < dimension; i++)
        {
            result[i] /= count;
        }

        return result;
    }
}
