using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.Services;

public interface IEmbeddingIndexingService
{
    Task UpsertPaperAbstractAsync(Paper paper, CancellationToken cancellationToken);
    Task UpsertPaperAbstractAsync(IEnumerable<Paper> papers, CancellationToken cancellationToken);
    Task UpsertSummaryAsync(PaperSummary summary, CancellationToken cancellationToken);
    Task UpsertDocumentChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken);
    Task UpsertDocumentChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken);
}

public sealed class EmbeddingIndexingService(
    ApplicationDbContext dbContext,
    ILocalEmbeddingClient embeddingClient,
    IOptions<LocalEmbeddingOptions> options,
    ILogger<EmbeddingIndexingService> logger) : IEmbeddingIndexingService
{
    private readonly LocalEmbeddingOptions _options = options.Value;

    public Task UpsertPaperAbstractAsync(Paper paper, CancellationToken cancellationToken)
        => UpsertPaperEmbeddingAsync(paper.Id, null, null, EmbeddingType.PaperAbstract, paper.Abstract, cancellationToken);

    public async Task UpsertPaperAbstractAsync(IEnumerable<Paper> papers, CancellationToken cancellationToken)
    {
        var paperList = papers.ToList();
        if (paperList.Count == 0)
            return;

        var embeddingsToAdd = new List<PaperEmbedding>();

        foreach (var paper in paperList)
        {
            var existing = await dbContext.PaperEmbeddings
                .FirstOrDefaultAsync(e =>
                    e.PaperId == paper.Id &&
                    e.SummaryId == null &&
                    e.DocumentChunkId == null &&
                    e.EmbeddingType == EmbeddingType.PaperAbstract,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(paper.Abstract))
            {
                if (existing is not null)
                {
                    dbContext.PaperEmbeddings.Remove(existing);
                }
                continue;
            }

            var vector = await embeddingClient.GenerateEmbeddingAsync(paper.Abstract.Trim(), cancellationToken);

            if (existing is null)
            {
                embeddingsToAdd.Add(new PaperEmbedding
                {
                    PaperId = paper.Id,
                    EmbeddingType = EmbeddingType.PaperAbstract,
                    ModelName = _options.ModelName,
                    Vector = vector
                });
            }
            else
            {
                existing.ModelName = _options.ModelName;
                existing.Vector = vector;
            }
        }

        if (embeddingsToAdd.Count > 0)
        {
            dbContext.PaperEmbeddings.AddRange(embeddingsToAdd);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Upserted {Count} paper abstract embeddings", paperList.Count);
    }

    public async Task UpsertSummaryAsync(PaperSummary summary, CancellationToken cancellationToken)
    {
        var paper = summary.Paper;
        if (paper is null && summary.PaperId != Guid.Empty)
        {
            paper = await dbContext.Papers.FirstOrDefaultAsync(p => p.Id == summary.PaperId, cancellationToken);
        }

        await UpsertPaperEmbeddingAsync(paper?.Id, summary.Id, null, EmbeddingType.Summary, summary.SearchText, cancellationToken);
    }

    public async Task UpsertDocumentChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken)
    {
        await UpsertDocumentChunkEmbeddingAsync(chunk, cancellationToken);
    }

    public async Task UpsertDocumentChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken)
    {
        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
            return;

        var embeddingsToAdd = new List<PaperEmbedding>();

        foreach (var chunk in chunkList)
        {
            var existing = await dbContext.PaperEmbeddings
                .FirstOrDefaultAsync(e =>
                    e.DocumentChunkId == chunk.Id &&
                    e.EmbeddingType == EmbeddingType.DocumentChunk,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(chunk.Text))
            {
                if (existing is not null)
                {
                    dbContext.PaperEmbeddings.Remove(existing);
                }
                continue;
            }

            var vector = await embeddingClient.GenerateEmbeddingAsync(chunk.Text.Trim(), cancellationToken);

            if (existing is null)
            {
                embeddingsToAdd.Add(new PaperEmbedding
                {
                    PaperId = chunk.PaperDocument?.PaperId ?? chunk.PaperDocumentId,
                    DocumentChunkId = chunk.Id,
                    EmbeddingType = EmbeddingType.DocumentChunk,
                    ModelName = _options.ModelName,
                    Vector = vector
                });
            }
            else
            {
                existing.PaperId = chunk.PaperDocument?.PaperId ?? chunk.PaperDocumentId;
                existing.DocumentChunkId = chunk.Id;
                existing.EmbeddingType = EmbeddingType.DocumentChunk;
                existing.ModelName = _options.ModelName;
                existing.Vector = vector;
            }
        }

        if (embeddingsToAdd.Count > 0)
        {
            dbContext.PaperEmbeddings.AddRange(embeddingsToAdd);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Upserted {Count} document chunk embeddings", chunkList.Count);
    }

    private async Task UpsertPaperEmbeddingAsync(
        Guid? paperId,
        Guid? summaryId,
        Guid? documentChunkId,
        EmbeddingType embeddingType,
        string? content,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.PaperEmbeddings
            .FirstOrDefaultAsync(e =>
                e.PaperId == paperId &&
                e.SummaryId == summaryId &&
                e.DocumentChunkId == documentChunkId &&
                e.EmbeddingType == embeddingType,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            if (existing is not null)
            {
                dbContext.PaperEmbeddings.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Removed embedding for {EmbeddingType} {TargetId}", embeddingType, paperId ?? summaryId ?? documentChunkId);
            }

            return;
        }

        var vector = await embeddingClient.GenerateEmbeddingAsync(content.Trim(), cancellationToken);

        if (existing is null)
        {
            existing = new PaperEmbedding
            {
                PaperId = paperId,
                SummaryId = summaryId,
                DocumentChunkId = documentChunkId,
                EmbeddingType = embeddingType
            };
            dbContext.PaperEmbeddings.Add(existing);
        }

        existing.PaperId = paperId;
        existing.SummaryId = summaryId;
        existing.DocumentChunkId = documentChunkId;
        existing.EmbeddingType = embeddingType;
        existing.ModelName = _options.ModelName;
        existing.Vector = vector;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Upserted embedding for {EmbeddingType} {TargetId}", embeddingType, paperId ?? summaryId ?? documentChunkId);
    }

    private async Task UpsertDocumentChunkEmbeddingAsync(DocumentChunk chunk, CancellationToken cancellationToken)
    {
        var existing = await dbContext.PaperEmbeddings
            .FirstOrDefaultAsync(e =>
                e.DocumentChunkId == chunk.Id &&
                e.EmbeddingType == EmbeddingType.DocumentChunk,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(chunk.Text))
        {
            if (existing is not null)
            {
                dbContext.PaperEmbeddings.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Removed embedding for DocumentChunk {ChunkId}", chunk.Id);
            }

            return;
        }

        var vector = await embeddingClient.GenerateEmbeddingAsync(chunk.Text.Trim(), cancellationToken);

        if (existing is null)
        {
            existing = new PaperEmbedding
            {
                PaperId = chunk.PaperDocument?.PaperId ?? chunk.PaperDocumentId,
                DocumentChunkId = chunk.Id,
                EmbeddingType = EmbeddingType.DocumentChunk
            };
            dbContext.PaperEmbeddings.Add(existing);
        }

        existing.PaperId = chunk.PaperDocument?.PaperId ?? chunk.PaperDocumentId;
        existing.DocumentChunkId = chunk.Id;
        existing.EmbeddingType = EmbeddingType.DocumentChunk;
        existing.ModelName = _options.ModelName;
        existing.Vector = vector;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Upserted embedding for DocumentChunk {ChunkId}", chunk.Id);
    }
}
