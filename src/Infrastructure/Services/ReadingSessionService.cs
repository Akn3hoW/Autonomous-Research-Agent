using System;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.ReadingSessions;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class ReadingSessionService(ApplicationDbContext dbContext) : IReadingSessionService
{
    public async Task<PagedResult<ReadingSessionModel>> ListAsync(ReadingSessionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryable = dbContext.PaperReadingSessions
            .AsNoTracking()
            .Include(x => x.Paper)
            .Where(x => x.UserId == query.UserId);

        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<ReadingStatus>(query.Status, true, out var status))
        {
            queryable = queryable.Where(x => x.Status == status);
        }

        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReadingSessionModel>(
            items.Select(MapToModel).ToList(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ReadingSessionModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaperReadingSessions
            .AsNoTracking()
            .Include(x => x.Paper)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<ReadingSessionModel> CreateAsync(CreateReadingSessionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await dbContext.PaperReadingSessions
            .FirstOrDefaultAsync(x => x.UserId == command.UserId && x.PaperId == command.PaperId, cancellationToken);

        if (existing is not null)
        {
            throw new ConflictException($"A reading session for this paper already exists.");
        }

        var paperExists = await dbContext.Papers.AnyAsync(p => p.Id == command.PaperId, cancellationToken);
        if (!paperExists)
        {
            throw new NotFoundException(nameof(Paper), command.PaperId);
        }

        var entity = new PaperReadingSession
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            PaperId = command.PaperId,
            Status = ReadingStatus.ToRead
        };

        dbContext.PaperReadingSessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var loaded = await dbContext.PaperReadingSessions
            .AsNoTracking()
            .Include(x => x.Paper)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return MapToModel(loaded);
    }

    public async Task<ReadingSessionModel> UpdateAsync(Guid id, UpdateReadingSessionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await dbContext.PaperReadingSessions
            .Include(x => x.Paper)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperReadingSession), id);

        if (!string.IsNullOrEmpty(command.Status))
        {
            if (!Enum.TryParse<ReadingStatus>(command.Status, true, out var newStatus))
            {
                throw new InvalidStateException($"Invalid status: {command.Status}");
            }

            var oldStatus = entity.Status;
            entity.Status = newStatus;

            if (newStatus == ReadingStatus.Reading && oldStatus != ReadingStatus.Reading)
            {
                entity.StartedAt = DateTimeOffset.UtcNow;
            }
            else if (newStatus == ReadingStatus.Read && oldStatus != ReadingStatus.Read)
            {
                entity.FinishedAt = DateTimeOffset.UtcNow;
            }
            else if (newStatus == ReadingStatus.ToRead)
            {
                entity.StartedAt = null;
                entity.FinishedAt = null;
            }
        }

        if (command.Notes is not null)
        {
            entity.Notes = command.Notes;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaperReadingSessions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PaperReadingSession), id);

        dbContext.PaperReadingSessions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ReadingSessionModel MapToModel(PaperReadingSession entity)
    {
        return new ReadingSessionModel(
            entity.Id,
            entity.UserId,
            entity.PaperId,
            entity.Paper?.Title ?? string.Empty,
            entity.Paper?.Authors ?? [],
            entity.Paper?.Year,
            entity.Paper?.Venue,
            entity.Paper?.CitationCount ?? 0,
            entity.Status.ToString(),
            entity.Notes,
            entity.StartedAt,
            entity.FinishedAt,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
