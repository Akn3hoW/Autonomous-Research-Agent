using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class PaperService(
    ApplicationDbContext dbContext,
    ISemanticScholarClient semanticScholarClient,
    IJobService jobService,
    ILogger<PaperService> logger) : IPaperService
{
    public async Task<PagedResult<PaperListItem>> ListAsync(PaperQuery query, CancellationToken cancellationToken)
    {
        var papersQuery = dbContext.Papers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var pattern = QueryHelpers.ToILikePattern(query.Query);
            papersQuery = papersQuery.Where(p =>
                EF.Functions.ILike(p.Title, pattern) ||
                (p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern)));
        }

        if (query.Year.HasValue)
        {
            papersQuery = papersQuery.Where(p => p.Year == query.Year);
        }

        if (!string.IsNullOrWhiteSpace(query.Venue))
        {
            var pattern = QueryHelpers.ToILikePattern(query.Venue);
            papersQuery = papersQuery.Where(p => p.Venue != null && EF.Functions.ILike(p.Venue, pattern));
        }

        if (query.Source.HasValue)
        {
            papersQuery = papersQuery.Where(p => p.Source == query.Source.Value);
        }

        if (query.Status.HasValue)
        {
            papersQuery = papersQuery.Where(p => p.Status == query.Status.Value);
        }

        papersQuery = ApplySorting(papersQuery, query);

        var totalCount = await papersQuery.LongCountAsync(cancellationToken);

        var papers = await papersQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = papers.Select(p => p.ToListItem()).ToList();

        return new PagedResult<PaperListItem>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<PaperDetail> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var paper = await dbContext.Papers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return paper is null
            ? throw new NotFoundException(nameof(Paper), id)
            : paper.ToDetail();
    }

    public async Task<PaperDetail> CreateAsync(CreatePaperCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Doi))
        {
            var duplicate = await dbContext.Papers.AnyAsync(
                p => p.Doi == command.Doi,
                cancellationToken);

            if (duplicate)
            {
                throw new ConflictException($"A paper with DOI '{command.Doi}' already exists.");
            }
        }

        var entity = new Paper
        {
            SemanticScholarId = command.SemanticScholarId,
            Doi = command.Doi,
            Title = command.Title.Trim(),
            Abstract = command.Abstract?.Trim(),
            Authors = SanitizeAuthors(command.Authors),
            Year = command.Year,
            Venue = command.Venue?.Trim(),
            CitationCount = command.CitationCount,
            Source = command.Source,
            Status = command.Status,
            MetadataJson = JsonNodeMapper.Serialize(command.Metadata)
        };

        dbContext.Papers.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created paper {PaperId}", entity.Id);
        return entity.ToDetail();
    }

    public async Task<PaperDetail> UpdateAsync(Guid id, UpdatePaperCommand command, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Papers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Paper), id);

        if (command.Doi is not null)
        {
            entity.Doi = string.IsNullOrWhiteSpace(command.Doi) ? null : command.Doi.Trim();
        }

        if (command.Title is not null)
        {
            entity.Title = command.Title.Trim();
        }

        if (command.Abstract is not null)
        {
            entity.Abstract = string.IsNullOrWhiteSpace(command.Abstract) ? null : command.Abstract.Trim();
        }

        if (command.Authors is not null)
        {
            entity.Authors = SanitizeAuthors(command.Authors);
        }

        if (command.Year.HasValue)
        {
            entity.Year = command.Year;
        }

        if (command.Venue is not null)
        {
            entity.Venue = string.IsNullOrWhiteSpace(command.Venue) ? null : command.Venue.Trim();
        }

        if (command.CitationCount.HasValue)
        {
            entity.CitationCount = command.CitationCount.Value;
        }

        if (command.Status.HasValue)
        {
            entity.Status = command.Status.Value;
        }

        if (command.Metadata is not null)
        {
            entity.MetadataJson = JsonNodeMapper.Serialize(command.Metadata);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated paper {PaperId}", entity.Id);

        return entity.ToDetail();
    }

    public async Task<ImportPapersResult> ImportAsync(ImportPapersCommand command, CancellationToken cancellationToken)
    {
        var imported = await semanticScholarClient.SearchPapersAsync(command.Queries, command.Limit, cancellationToken);

        var semanticIds = imported.Select(c => c.SemanticScholarId).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var dois = imported.Select(c => c.Doi).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        var existingPapers = await dbContext.Papers
            .Where(p => (p.SemanticScholarId != null && semanticIds.Contains(p.SemanticScholarId))
                     || (p.Doi != null && dois.Contains(p.Doi)))
            .ToListAsync(cancellationToken);

        var bySemanticId = existingPapers
            .Where(p => p.SemanticScholarId != null)
            .ToDictionary(p => p.SemanticScholarId!);
        var byDoi = existingPapers
            .Where(p => p.Doi != null)
            .ToDictionary(p => p.Doi!);

        var candidatePdfUrls = imported
            .Select(c => c.Metadata?["openAccessPdfUrl"]?.GetValue<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToHashSet();

        var existingDocUrls = candidatePdfUrls.Count > 0
            ? (await dbContext.PaperDocuments
                .Where(d => candidatePdfUrls.Contains(d.SourceUrl))
                .Select(d => new { d.PaperId, d.SourceUrl })
                .ToListAsync(cancellationToken))
                .Select(d => (d.PaperId, d.SourceUrl))
                .ToHashSet()
            : new HashSet<(Guid, string)>();

        var results = new List<PaperDetail>();
        var queuedDocuments = new List<PaperDocument>();
        var newCount = 0;

        foreach (var candidate in imported)
        {
            Paper? existing = null;
            if (!string.IsNullOrWhiteSpace(candidate.SemanticScholarId))
                bySemanticId.TryGetValue(candidate.SemanticScholarId, out existing);
            if (existing is null && !string.IsNullOrWhiteSpace(candidate.Doi))
                byDoi.TryGetValue(candidate.Doi, out existing);

            if (existing is null)
            {
                existing = new Paper
                {
                    SemanticScholarId = candidate.SemanticScholarId,
                    Doi = candidate.Doi,
                };
                ApplyImportFields(existing, candidate);

                if (command.StoreImportedPapers)
                {
                    dbContext.Papers.Add(existing);
                }

                newCount++;
            }
            else if (command.StoreImportedPapers)
            {
                ApplyImportFields(existing, candidate);
            }

            if (command.StoreImportedPapers)
            {
                var attachedDocument = TryAttachImportedDocument(existing, candidate, existingDocUrls);
                if (attachedDocument is not null)
                {
                    queuedDocuments.Add(attachedDocument);
                }
            }

            results.Add(existing.ToDetail());
        }

        if (command.StoreImportedPapers)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var document in queuedDocuments)
            {
                document.Status = PaperDocumentStatus.Queued;
                document.LastError = null;

                await jobService.CreateAsync(
                    new CreateJobCommand(JobType.ProcessPaperDocument, PaperDocumentJobPayload.Create(document), document.Id, "system-import"),
                    cancellationToken);
            }

            if (queuedDocuments.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        logger.LogInformation("Imported {Count} papers ({New} new) from Semantic Scholar", results.Count, newCount);
        return new ImportPapersResult(results, newCount);
    }


    private PaperDocument? TryAttachImportedDocument(Paper paper, SemanticScholarPaperImportModel candidate, HashSet<(Guid PaperId, string SourceUrl)> existingDocUrls)
    {
        var openAccessPdfUrl = candidate.Metadata?["openAccessPdfUrl"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(openAccessPdfUrl))
        {
            return null;
        }

        if (existingDocUrls.Contains((paper.Id, openAccessPdfUrl)))
        {
            return null;
        }

        var document = new PaperDocument
        {
            PaperId = paper.Id,
            SourceUrl = openAccessPdfUrl,
            FileName = $"{paper.Id:N}.pdf",
            MediaType = "application/pdf",
            Status = PaperDocumentStatus.Pending
        };

        dbContext.PaperDocuments.Add(document);
        return document;
    }

    private static void ApplyImportFields(Paper entity, SemanticScholarPaperImportModel candidate)
    {
        entity.Title = candidate.Title;
        entity.Abstract = candidate.Abstract;
        entity.Authors = SanitizeAuthors(candidate.Authors);
        entity.Year = candidate.Year;
        entity.Venue = candidate.Venue;
        entity.CitationCount = candidate.CitationCount;
        entity.Source = PaperSource.SemanticScholar;
        entity.Status = PaperStatus.Imported;
        entity.MetadataJson = JsonNodeMapper.Serialize(candidate.Metadata);
    }

    private static List<string> SanitizeAuthors(IEnumerable<string> authors) =>
        authors.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();

    private static IQueryable<Paper> ApplySorting(IQueryable<Paper> queryable, PaperQuery query)
    {
        var sortBy = query.SortBy?.Trim().ToLowerInvariant();
        var descending = query.SortDirection == SortDirection.Desc;

        return sortBy switch
        {
            "title" => descending
                ? queryable.OrderByDescending(p => p.Title)
                : queryable.OrderBy(p => p.Title),
            "year" => descending
                ? queryable.OrderByDescending(p => p.Year)
                : queryable.OrderBy(p => p.Year),
            "citationcount" => descending
                ? queryable.OrderByDescending(p => p.CitationCount)
                : queryable.OrderBy(p => p.CitationCount),
            _ => descending
                ? queryable.OrderByDescending(p => p.UpdatedAt)
                : queryable.OrderBy(p => p.UpdatedAt)
        };
    }
}
