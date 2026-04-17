using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class SummaryService(
    ApplicationDbContext dbContext,
    IEmbeddingIndexingService embeddingIndexingService,
    ILogger<SummaryService> logger) : ISummaryService
{
    public async Task<IReadOnlyCollection<SummaryModel>> ListForPaperAsync(Guid paperId, CancellationToken cancellationToken)
    {
        var items = await dbContext.PaperSummaries
            .AsNoTracking()
            .Where(s => s.PaperId == paperId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            var paperExists = await dbContext.Papers.AnyAsync(p => p.Id == paperId, cancellationToken);
            if (!paperExists)
            {
                throw new NotFoundException(nameof(Paper), paperId);
            }
        }

        return items.Select(s => s.ToModel()).ToList();
    }

    public async Task<SummaryModel> GetByIdAsync(Guid summaryId, CancellationToken cancellationToken)
    {
        var summary = await dbContext.PaperSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == summaryId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperSummary), summaryId);

        return summary.ToModel();
    }

    public async Task<SummaryModel> CreateAsync(CreateSummaryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var paperExists = await dbContext.Papers.AnyAsync(p => p.Id == command.PaperId, cancellationToken);
        if (!paperExists)
        {
            throw new NotFoundException(nameof(Paper), command.PaperId);
        }

        var entity = new PaperSummary
        {
            PaperId = command.PaperId,
            ModelName = command.ModelName.Trim(),
            PromptVersion = command.PromptVersion.Trim(),
            Status = command.Status,
            SummaryJson = JsonNodeMapper.Serialize(command.Summary),
            SearchText = command.SearchText,
            AbTestSessionId = command.AbTestSessionId,
            IsSelected = command.IsSelected
        };

        dbContext.PaperSummaries.Add(entity);
        await embeddingIndexingService.UpsertSummaryAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created summary {SummaryId} for paper {PaperId}", entity.Id, entity.PaperId);
        return entity.ToModel();
    }

    public async Task<SummaryModel> UpdateAsync(Guid summaryId, UpdateSummaryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var entity = await dbContext.PaperSummaries.FirstOrDefaultAsync(s => s.Id == summaryId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperSummary), summaryId);

        if (command.Status.HasValue)
        {
            entity.Status = command.Status.Value;
        }

        if (command.Summary is not null)
        {
            entity.SummaryJson = JsonNodeMapper.Serialize(command.Summary);
        }

        if (command.SearchText is not null)
        {
            entity.SearchText = command.SearchText;
        }

        await embeddingIndexingService.UpsertSummaryAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated summary {SummaryId}", entity.Id);

        return entity.ToModel();
    }

    public async Task<SummaryModel> ReviewAsync(Guid summaryId, ReviewSummaryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var entity = await dbContext.PaperSummaries.FirstOrDefaultAsync(s => s.Id == summaryId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperSummary), summaryId);

        if (entity.Status is SummaryStatus.Approved or SummaryStatus.Rejected)
        {
            throw new InvalidStateException("Summary is already in a terminal review state.");
        }

        if (command.Status is not SummaryStatus.Approved and not SummaryStatus.Rejected)
        {
            throw new InvalidStateException("Review operations can only approve or reject a summary.");
        }

        entity.Status = command.Status;
        entity.ReviewedBy = command.Reviewer;
        entity.ReviewedAt = DateTimeOffset.UtcNow;
        entity.ReviewNotes = command.Notes;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Reviewed summary {SummaryId} with status {Status}", entity.Id, entity.Status);

        return entity.ToModel();
    }

    public async Task DeleteAsync(Guid summaryId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PaperSummaries.FirstOrDefaultAsync(s => s.Id == summaryId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperSummary), summaryId);

        dbContext.PaperSummaries.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted summary {SummaryId}", summaryId);
    }
}
