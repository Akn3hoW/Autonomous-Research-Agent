using AutonomousResearchAgent.Application.Citations;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class CitationGraphService(
    ApplicationDbContext dbContext,
    ISemanticScholarClient semanticScholarClient,
    ILogger<CitationGraphService> logger) : ICitationGraphService
{
    public async Task<CitationGraphDto> GetCitationGraphAsync(Guid paperId, int depth, CancellationToken cancellationToken)
    {
        var paper = await dbContext.Papers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paperId, cancellationToken);

        if (paper is null)
        {
            throw new NotFoundException(nameof(Paper), paperId);
        }

        var visitedPaperIds = new HashSet<Guid> { paperId };
        var nodes = new Dictionary<Guid, PaperNode>();
        var edges = new List<CitationEdge>();
        var idToNodeId = new Dictionary<Guid, int>();
        var nextNodeId = 0;

        idToNodeId[paperId] = nextNodeId++;
        nodes[paperId] = new PaperNode(
            idToNodeId[paperId],
            paper.Title,
            paper.Year,
            paper.CitationCount,
            true);

        await BuildGraphAsync(paper, depth, visitedPaperIds, nodes, edges, idToNodeId, cancellationToken);

        return new CitationGraphDto(
            nodes.Values.ToList(),
            edges);
    }

    private async Task BuildGraphAsync(
        Paper paper,
        int remainingDepth,
        HashSet<Guid> visitedPaperIds,
        Dictionary<Guid, PaperNode> nodes,
        List<CitationEdge> edges,
        Dictionary<Guid, int> idToNodeId,
        CancellationToken cancellationToken)
    {
        if (remainingDepth <= 0) return;

        var sourceNodeId = idToNodeId[paper.Id];
        var newNodeId = nodes.Count;

        var citationTargetIds = await dbContext.PaperCitations
            .AsNoTracking()
            .Where(c => c.SourcePaperId == paper.Id)
            .Select(c => c.TargetPaperId)
            .ToListAsync(cancellationToken);

        var citationList = citationTargetIds.Count > 0
            ? await dbContext.PaperCitations
                .AsNoTracking()
                .Where(c => c.SourcePaperId == paper.Id)
                .Select(c => new { c.TargetPaperId, c.CitationContext })
                .ToListAsync(cancellationToken)
            : null;

        var citations = citationList ?? [];

        var citedPaperIds = citations.Select(c => c.TargetPaperId).ToList();
        var citedPapers = citedPaperIds.Count > 0
            ? await dbContext.Papers
                .AsNoTracking()
                .Where(p => citedPaperIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken)
            : new Dictionary<Guid, Paper>();

        foreach (var citation in citations)
        {
            if (!citedPapers.TryGetValue(citation.TargetPaperId, out var targetPaper))
                continue;

            if (visitedPaperIds.Add(targetPaper.Id))
            {
                idToNodeId[targetPaper.Id] = nodes.Count;
                nodes[targetPaper.Id] = new PaperNode(
                    idToNodeId[targetPaper.Id],
                    targetPaper.Title,
                    targetPaper.Year,
                    targetPaper.CitationCount,
                    true);

                edges.Add(new CitationEdge(
                    sourceNodeId,
                    idToNodeId[targetPaper.Id],
                    citation.CitationContext));

                await BuildGraphAsync(targetPaper, remainingDepth - 1, visitedPaperIds, nodes, edges, idToNodeId, cancellationToken);
            }
        }

        var referenceSourceIds = await dbContext.PaperCitations
            .AsNoTracking()
            .Where(c => c.TargetPaperId == paper.Id)
            .Select(c => c.SourcePaperId)
            .ToListAsync(cancellationToken);

        var referenceList = referenceSourceIds.Count > 0
            ? await dbContext.PaperCitations
                .AsNoTracking()
                .Where(c => c.TargetPaperId == paper.Id)
                .Select(c => new { c.SourcePaperId, c.CitationContext })
                .ToListAsync(cancellationToken)
            : null;

        var references = referenceList ?? [];

        var referencerPaperIds = references.Select(r => r.SourcePaperId).ToList();
        var referencerPapers = referencerPaperIds.Count > 0
            ? await dbContext.Papers
                .AsNoTracking()
                .Where(p => referencerPaperIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken)
            : new Dictionary<Guid, Paper>();

        foreach (var reference in references)
        {
            if (!referencerPapers.TryGetValue(reference.SourcePaperId, out var sourcePaper))
                continue;

            if (visitedPaperIds.Add(sourcePaper.Id))
            {
                idToNodeId[sourcePaper.Id] = nodes.Count;
                nodes[sourcePaper.Id] = new PaperNode(
                    idToNodeId[sourcePaper.Id],
                    sourcePaper.Title,
                    sourcePaper.Year,
                    sourcePaper.CitationCount,
                    true);

                edges.Add(new CitationEdge(
                    idToNodeId[sourcePaper.Id],
                    sourceNodeId,
                    reference.CitationContext));

                await BuildGraphAsync(sourcePaper, remainingDepth - 1, visitedPaperIds, nodes, edges, idToNodeId, cancellationToken);
            }
        }
    }

    public async Task IngestCitationsAsync(Guid paperId, CancellationToken cancellationToken)
    {
        var paper = await dbContext.Papers
            .FirstOrDefaultAsync(p => p.Id == paperId, cancellationToken);

        if (paper is null)
        {
            throw new NotFoundException(nameof(Paper), paperId);
        }

        if (string.IsNullOrWhiteSpace(paper.SemanticScholarId))
        {
            logger.LogWarning("Paper {PaperId} has no SemanticScholarId, skipping citation ingestion", paperId);
            return;
        }

        var details = await semanticScholarClient.GetPaperDetailsAsync(paper.SemanticScholarId, cancellationToken);
        if (details is null)
        {
            logger.LogWarning("Could not fetch citation details for paper {PaperId}", paperId);
            return;
        }

        var existingCitationTargets = await dbContext.PaperCitations
            .Where(c => c.SourcePaperId == paperId)
            .Select(c => c.TargetPaperId)
            .ToListAsync(cancellationToken);

        var citationPaperIds = details.Citations
            .Select(c => c.SemanticScholarId)
            .ToHashSet();

        var existingPapers = await dbContext.Papers
            .Where(p => p.SemanticScholarId != null && citationPaperIds.Contains(p.SemanticScholarId))
            .ToDictionaryAsync(p => p.SemanticScholarId!, cancellationToken);

        var newPapers = new List<Paper>();
        var paperIdBySemanticId = existingPapers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id);
        var newCitations = new List<PaperCitation>();

        foreach (var citation in details.Citations)
        {
            if (paperIdBySemanticId.ContainsKey(citation.SemanticScholarId))
            {
                continue;
            }

            var newPaper = new Paper
            {
                SemanticScholarId = citation.SemanticScholarId,
                Title = citation.Title,
                Year = citation.Year,
                CitationCount = 0,
                Source = Domain.Enums.PaperSource.SemanticScholar,
                Status = Domain.Enums.PaperStatus.Imported
            };
            newPapers.Add(newPaper);
        }

        var referencePaperIds = details.References
            .Select(r => r.SemanticScholarId)
            .ToHashSet();

        var refExistingPapers = await dbContext.Papers
            .Where(p => p.SemanticScholarId != null && referencePaperIds.Contains(p.SemanticScholarId))
            .ToDictionaryAsync(p => p.SemanticScholarId!, cancellationToken);

        var existingReferenceSources = await dbContext.PaperCitations
            .Where(c => c.TargetPaperId == paperId)
            .Select(c => c.SourcePaperId)
            .ToListAsync(cancellationToken);

        foreach (var reference in details.References)
        {
            if (refExistingPapers.TryGetValue(reference.SemanticScholarId, out var existingRefPaper))
            {
                paperIdBySemanticId[reference.SemanticScholarId] = existingRefPaper.Id;
            }
            else if (!paperIdBySemanticId.ContainsKey(reference.SemanticScholarId))
            {
                var newPaper = new Paper
                {
                    SemanticScholarId = reference.SemanticScholarId,
                    Title = reference.Title,
                    Year = reference.Year,
                    CitationCount = 0,
                    Source = Domain.Enums.PaperSource.SemanticScholar,
                    Status = Domain.Enums.PaperStatus.Imported
                };
                newPapers.Add(newPaper);
            }
        }

        if (newPapers.Count > 0)
        {
            dbContext.Papers.AddRange(newPapers);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var newPaper in newPapers)
            {
                paperIdBySemanticId[newPaper.SemanticScholarId!] = newPaper.Id;
            }
        }

        var existingCitationTargetsSet = existingCitationTargets.ToHashSet();

        foreach (var citation in details.Citations)
        {
            if (!paperIdBySemanticId.TryGetValue(citation.SemanticScholarId, out var targetPaperId))
                continue;

            if (existingCitationTargetsSet.Contains(targetPaperId))
                continue;

            newCitations.Add(new PaperCitation
            {
                SourcePaperId = paperId,
                TargetPaperId = targetPaperId,
                CitationContext = citation.Context,
                IngestedAt = DateTime.UtcNow
            });
        }

        var existingReferenceSourcesSet = existingReferenceSources.ToHashSet();

        foreach (var reference in details.References)
        {
            if (!paperIdBySemanticId.TryGetValue(reference.SemanticScholarId, out var sourcePaperId))
                continue;

            if (existingReferenceSourcesSet.Contains(sourcePaperId))
                continue;

            newCitations.Add(new PaperCitation
            {
                SourcePaperId = sourcePaperId,
                TargetPaperId = paperId,
                CitationContext = reference.Context,
                IngestedAt = DateTime.UtcNow
            });
        }

        if (newCitations.Count > 0)
        {
            dbContext.PaperCitations.AddRange(newCitations);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Ingested {Count} citations for paper {PaperId}", newCitations.Count, paperId);
        }
    }
}
