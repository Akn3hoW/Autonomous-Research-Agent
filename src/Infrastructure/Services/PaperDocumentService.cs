using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class PaperDocumentService(
    ApplicationDbContext dbContext,
    IJobService jobService,
    ILogger<PaperDocumentService> logger) : IPaperDocumentService
{
    public async Task<IReadOnlyCollection<PaperDocumentModel>> ListByPaperIdAsync(Guid paperId, CancellationToken cancellationToken)
    {
        await EnsurePaperExistsAsync(paperId, cancellationToken);

        var documents = await dbContext.PaperDocuments
            .AsNoTracking()
            .Where(d => d.PaperId == paperId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(d => d.ToModel()).ToList();
    }

    public async Task<PaperDocumentModel> GetByIdAsync(Guid paperId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.PaperDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.PaperId == paperId && d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperDocument), documentId);

        return document.ToModel();
    }

    public async Task<PaperDocumentModel> CreateAsync(CreatePaperDocumentCommand command, CancellationToken cancellationToken)
    {
        await EnsurePaperExistsAsync(command.PaperId, cancellationToken);

        var normalizedUrl = command.SourceUrl.Trim();
        var existing = await dbContext.PaperDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.PaperId == command.PaperId && d.SourceUrl == normalizedUrl, cancellationToken);

        if (existing is not null)
        {
            throw new ConflictException($"A document with source URL {normalizedUrl} already exists for this paper.");
        }

        var entity = new PaperDocument
        {
            PaperId = command.PaperId,
            SourceUrl = normalizedUrl,
            FileName = string.IsNullOrWhiteSpace(command.FileName) ? null : command.FileName.Trim(),
            MediaType = string.IsNullOrWhiteSpace(command.MediaType) ? null : command.MediaType.Trim(),
            RequiresOcr = command.RequiresOcr,
            MetadataJson = JsonNodeMapper.Serialize(command.Metadata),
            Status = PaperDocumentStatus.Pending
        };

        dbContext.PaperDocuments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created paper document {DocumentId} for paper {PaperId}", entity.Id, entity.PaperId);
        return entity.ToModel();
    }

    public async Task<PaperDocumentModel> QueueProcessingAsync(Guid paperId, Guid documentId, QueuePaperDocumentProcessingCommand command, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PaperDocuments
            .FirstOrDefaultAsync(d => d.PaperId == paperId && d.Id == documentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperDocument), documentId);

        if (!command.Force && entity.Status == PaperDocumentStatus.Queued)
        {
            throw new ConflictException("Document processing is already queued.");
        }

        entity.Status = PaperDocumentStatus.Queued;
        entity.LastError = null;

        await jobService.CreateAsync(
            new CreateJobCommand(JobType.ProcessPaperDocument, PaperDocumentJobPayload.Create(entity), documentId, command.RequestedBy),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued processing for paper document {DocumentId}", entity.Id);

        return entity.ToModel();
    }

    private async Task EnsurePaperExistsAsync(Guid paperId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Papers.AnyAsync(p => p.Id == paperId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(Paper), paperId);
        }
    }
}
