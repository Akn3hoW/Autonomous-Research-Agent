using AutonomousResearchAgent.Application.Annotations;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class AnnotationService(ApplicationDbContext dbContext) : IAnnotationService
{
    public async Task<IReadOnlyCollection<AnnotationModel>> ListForPaperAsync(Guid paperId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PaperAnnotations
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.PaperId == paperId);

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(a => a.ToModel()).ToList();
    }

    public async Task<AnnotationModel> GetByIdAsync(Guid annotationId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PaperAnnotations
            .AsNoTracking()
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == annotationId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperAnnotation), annotationId);

        return entity.ToModel();
    }

    public async Task<AnnotationModel> CreateAsync(CreateAnnotationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var paperExists = await dbContext.Papers.AnyAsync(p => p.Id == command.PaperId, cancellationToken);
        if (!paperExists)
        {
            throw new NotFoundException(nameof(Paper), command.PaperId);
        }

        var entity = new PaperAnnotation
        {
            Id = Guid.NewGuid(),
            PaperId = command.PaperId,
            UserId = command.UserId,
            DocumentChunkId = command.DocumentChunkId,
            PageNumber = command.PageNumber,
            OffsetStart = command.OffsetStart,
            OffsetEnd = command.OffsetEnd,
            HighlightedText = command.HighlightedText,
            Note = command.Note
        };

        dbContext.PaperAnnotations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        entity.User = user ?? throw new NotFoundException(nameof(User), command.UserId);

        return entity.ToModel();
    }

    public async Task<AnnotationModel> UpdateAsync(Guid annotationId, UpdateAnnotationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var entity = await dbContext.PaperAnnotations
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == annotationId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperAnnotation), annotationId);

        if (command.Note is not null)
        {
            entity.Note = command.Note;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToModel();
    }

    public async Task DeleteAsync(Guid annotationId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.PaperAnnotations
            .FirstOrDefaultAsync(a => a.Id == annotationId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperAnnotation), annotationId);

        dbContext.PaperAnnotations.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}