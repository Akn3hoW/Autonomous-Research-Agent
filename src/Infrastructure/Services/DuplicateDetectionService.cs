using System.Text.Json;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Duplicates;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class DuplicateDetectionService(
    ApplicationDbContext dbContext,
    IOptions<DuplicateDetectionOptions> options,
    ILogger<DuplicateDetectionService> logger) : IDuplicateDetectionService
{
    private const int BatchSize = 100;
    private DuplicateDetectionOptions Options => options.Value;

    public async Task<Guid> StartDuplicateDetectionJobAsync(double threshold = 0.95, CancellationToken cancellationToken = default)
    {
        var job = new Job
        {
            Type = JobType.DuplicateDetection,
            Status = JobStatus.Queued,
            PayloadJson = JsonSerializer.Serialize(new { threshold })
        };

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created duplicate detection job {JobId} with threshold {Threshold}", job.Id, threshold);
        return job.Id;
    }

    public async Task<DuplicatesResult> GetPotentialDuplicatesAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PotentialDuplicates
            .AsNoTracking()
            .Include(d => d.PaperA)
            .Include(d => d.PaperB)
            .AsQueryable();

        var totalCount = await query.LongCountAsync(cancellationToken);
        var pendingCount = await query.CountAsync(d => d.Status == DuplicateReviewStatus.Pending, cancellationToken);

        var entities = await query
            .OrderByDescending(d => d.SimilarityScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pairs = entities.Select(d => new DuplicatePairModel(
            d.Id,
            d.PaperAId,
            d.PaperA?.Title ?? string.Empty,
            d.PaperBId,
            d.PaperB?.Title ?? string.Empty,
            d.SimilarityScore,
            d.Status,
            d.ReviewedByUserId,
            d.ReviewedAt,
            d.Notes,
            d.CreatedAt)).ToList();

        return new DuplicatesResult(pairs, (int)totalCount, pendingCount);
    }

    public async Task<DuplicatesResult> GetPendingDuplicatesAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PotentialDuplicates
            .AsNoTracking()
            .Include(d => d.PaperA)
            .Include(d => d.PaperB)
            .Where(d => d.Status == DuplicateReviewStatus.Pending);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var entities = await query
            .OrderByDescending(d => d.SimilarityScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pairs = entities.Select(d => new DuplicatePairModel(
            d.Id,
            d.PaperAId,
            d.PaperA?.Title ?? string.Empty,
            d.PaperBId,
            d.PaperB?.Title ?? string.Empty,
            d.SimilarityScore,
            d.Status,
            d.ReviewedByUserId,
            d.ReviewedAt,
            d.Notes,
            d.CreatedAt)).ToList();

        return new DuplicatesResult(pairs, (int)totalCount, (int)totalCount);
    }

    public async Task ResolveDuplicateAsync(Guid duplicateId, bool isDuplicate, Guid? mergedIntoPaperId, string? notes, Guid? reviewedByUserId, CancellationToken cancellationToken = default)
    {
        var duplicate = await dbContext.PotentialDuplicates.FindAsync(new object[] { duplicateId }, cancellationToken)
            ?? throw new NotFoundException(nameof(PotentialDuplicate), duplicateId);

        duplicate.Status = isDuplicate ? DuplicateReviewStatus.ConfirmedDuplicate : DuplicateReviewStatus.ConfirmedNotDuplicate;
        duplicate.ReviewedByUserId = reviewedByUserId;
        duplicate.ReviewedAt = DateTimeOffset.UtcNow;
        duplicate.Notes = notes;

        if (isDuplicate && mergedIntoPaperId.HasValue)
        {
            duplicate.Status = DuplicateReviewStatus.Merged;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Resolved duplicate pair {DuplicateId} as {Status}", duplicateId, duplicate.Status);
    }

    public async Task MergeDuplicatePapersAsync(Guid keepPaperId, Guid mergeIntoPaperId, string? notes, Guid? reviewedByUserId, CancellationToken cancellationToken = default)
    {
        var duplicate = await dbContext.PotentialDuplicates
            .FirstOrDefaultAsync(d =>
                (d.PaperAId == keepPaperId && d.PaperBId == mergeIntoPaperId) ||
                (d.PaperAId == mergeIntoPaperId && d.PaperBId == keepPaperId),
                cancellationToken);

        if (duplicate != null)
        {
            duplicate.Status = DuplicateReviewStatus.Merged;
            duplicate.ReviewedByUserId = reviewedByUserId;
            duplicate.ReviewedAt = DateTimeOffset.UtcNow;
            duplicate.Notes = notes;
        }

        var papersToMerge = await dbContext.Papers
            .Where(p => p.Id == keepPaperId || p.Id == mergeIntoPaperId)
            .ToListAsync(cancellationToken);

        var keepPaper = papersToMerge.FirstOrDefault(p => p.Id == keepPaperId);
        var mergePaper = papersToMerge.FirstOrDefault(p => p.Id == mergeIntoPaperId);

        if (keepPaper == null || mergePaper == null)
        {
            throw new NotFoundException(nameof(Paper), keepPaperId);
        }

        var summariesToUpdate = await dbContext.PaperSummaries
            .Where(s => s.PaperId == mergeIntoPaperId)
            .ToListAsync(cancellationToken);

        foreach (var summary in summariesToUpdate)
        {
            summary.PaperId = keepPaperId;
        }

        var documentsToUpdate = await dbContext.PaperDocuments
            .Where(d => d.PaperId == mergeIntoPaperId)
            .ToListAsync(cancellationToken);

        foreach (var document in documentsToUpdate)
        {
            document.PaperId = keepPaperId;
        }

        var embeddingsToUpdate = await dbContext.PaperEmbeddings
            .Where(e => e.PaperId == mergeIntoPaperId)
            .ToListAsync(cancellationToken);

        foreach (var embedding in embeddingsToUpdate)
        {
            embedding.PaperId = keepPaperId;
        }

        var citationsWhereFrom = await dbContext.PaperCitations
            .Where(c => c.SourcePaperId == mergeIntoPaperId)
            .ToListAsync(cancellationToken);

        foreach (var citation in citationsWhereFrom)
        {
            citation.SourcePaperId = keepPaperId;
        }

        var citationsWhereTo = await dbContext.PaperCitations
            .Where(c => c.TargetPaperId == mergeIntoPaperId)
            .ToListAsync(cancellationToken);

        foreach (var citation in citationsWhereTo)
        {
            citation.TargetPaperId = keepPaperId;
        }

        dbContext.Papers.Remove(mergePaper);

        if (duplicate != null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var newDuplicate = new PotentialDuplicate
            {
                PaperAId = keepPaperId,
                PaperBId = mergeIntoPaperId,
                SimilarityScore = 1.0,
                Status = DuplicateReviewStatus.Merged,
                ReviewedByUserId = reviewedByUserId,
                ReviewedAt = DateTimeOffset.UtcNow,
                Notes = notes
            };
            dbContext.PotentialDuplicates.Add(newDuplicate);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Merged paper {MergePaperId} into {KeepPaperId}", mergeIntoPaperId, keepPaperId);
    }

    public async Task ComputeDuplicatePairsAsync(double threshold, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting duplicate detection with threshold {Threshold}", threshold);

        var topK = Options.TopK;
        var maxComparisonsPerBatch = Options.MaxComparisonsPerBatch;

        var allEmbeddings = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Where(e => e.PaperId != null && e.Vector != null && e.EmbeddingType == EmbeddingType.PaperAbstract)
            .Select(e => new { e.PaperId, e.Vector })
            .ToListAsync(cancellationToken);

        var paperEmbeddings = allEmbeddings
            .GroupBy(e => e.PaperId)
            .Select(g => g.First())
            .Where(pe => pe.Vector != null)
            .ToList();

        logger.LogInformation("Found {Count} paper embeddings to compare", paperEmbeddings.Count);

        var existingDuplicates = await dbContext.PotentialDuplicates
            .AsNoTracking()
            .Select(d => new { d.PaperAId, d.PaperBId })
            .ToListAsync(cancellationToken);

        var existingPairs = existingDuplicates
            .Select(d => d.PaperAId < d.PaperBId ? (d.PaperAId, d.PaperBId) : (d.PaperBId, d.PaperAId))
            .ToHashSet();

        var newDuplicates = new List<PotentialDuplicate>();
        var processedPairs = 0;

        for (var i = 0; i < paperEmbeddings.Count; i++)
        {
            var embeddingA = paperEmbeddings[i];
            if (embeddingA.PaperId == null)
                continue;

            var paperIdA = embeddingA.PaperId.Value;
            var candidates = new List<(Guid PaperId, float[] Vector)>();
            for (var j = 0; j < paperEmbeddings.Count; j++)
            {
                if (i == j) continue;
                var embeddingB = paperEmbeddings[j];
                if (embeddingB.PaperId == null) continue;
                candidates.Add((embeddingB.PaperId.Value, embeddingB.Vector!));
            }

            var topCandidates = candidates
                .OrderByDescending(c => VectorMath.CosineSimilarity(embeddingA.Vector!, c.Vector))
                .Take(topK)
                .ToList();

            var batchComparisons = 0;
            foreach (var candidate in topCandidates)
            {
                if (batchComparisons >= maxComparisonsPerBatch)
                    break;

                var pairKey = paperIdA < candidate.PaperId ? (paperIdA, candidate.PaperId) : (candidate.PaperId, paperIdA);

                if (existingPairs.Contains(pairKey))
                    continue;

                var similarity = VectorMath.CosineSimilarity(embeddingA.Vector!, candidate.Vector);

                if (similarity >= threshold)
                {
                    newDuplicates.Add(new PotentialDuplicate
                    {
                        PaperAId = paperIdA,
                        PaperBId = candidate.PaperId,
                        SimilarityScore = similarity,
                        Status = DuplicateReviewStatus.Pending
                    });

                    existingPairs.Add(pairKey);
                    batchComparisons++;

                    if (newDuplicates.Count >= BatchSize)
                    {
                        dbContext.PotentialDuplicates.AddRange(newDuplicates);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        processedPairs += newDuplicates.Count;
                        logger.LogInformation("Saved batch of {BatchSize} duplicate pairs (total processed: {Processed})", newDuplicates.Count, processedPairs);
                        newDuplicates.Clear();
                    }
                }
            }
        }

        if (newDuplicates.Count > 0)
        {
            dbContext.PotentialDuplicates.AddRange(newDuplicates);
            await dbContext.SaveChangesAsync(cancellationToken);
            processedPairs += newDuplicates.Count;
        }

        logger.LogInformation("Completed duplicate detection. Found {Count} new potential duplicates", processedPairs);
    }
}
