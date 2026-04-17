using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Hypotheses;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class HypothesisService(
    ApplicationDbContext dbContext,
    ILogger<HypothesisService> logger) : IHypothesisService
{
    public async Task<HypothesisResponse> CreateAsync(CreateHypothesisCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = new Hypothesis
        {
            Title = command.Title,
            Description = command.Description,
            UserId = command.UserId,
            Status = HypothesisStatus.Proposed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (command.InitialPapers != null)
        {
            foreach (var paper in command.InitialPapers)
            {
                var hp = new HypothesisPaper
                {
                    HypothesisId = entity.Id,
                    PaperId = paper.PaperId,
                    EvidenceType = paper.EvidenceType,
                    EvidenceText = paper.EvidenceText
                };
                entity.HypothesisPapers.Add(hp);
            }
        }

        dbContext.Hypotheses.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created hypothesis {HypothesisId} for user {UserId}", entity.Id, entity.UserId);
        return ToResponse(entity);
    }

    public async Task<HypothesisResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Hypotheses
            .AsNoTracking()
            .Include(h => h.HypothesisPapers)
                .ThenInclude(hp => hp.Paper)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

        return entity == null ? null : ToResponse(entity);
    }

    public async Task<IReadOnlyList<HypothesisResponse>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await dbContext.Hypotheses
            .AsNoTracking()
            .Include(h => h.HypothesisPapers)
                .ThenInclude(hp => hp.Paper)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToResponse).ToList();
    }

    public async Task<HypothesisResponse> UpdateAsync(Guid id, UpdateHypothesisCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await dbContext.Hypotheses
            .Include(h => h.HypothesisPapers)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Hypothesis), id);

        if (command.Title != null)
            entity.Title = command.Title;
        if (command.Description != null)
            entity.Description = command.Description;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated hypothesis {HypothesisId}", id);

        return ToResponse(entity);
    }

    public async Task<HypothesisResponse> UpdateStatusAsync(Guid id, UpdateHypothesisStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await dbContext.Hypotheses
            .Include(h => h.HypothesisPapers)
                .ThenInclude(hp => hp.Paper)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Hypothesis), id);

        entity.Status = command.Status;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (command.EvidenceText != null)
        {
            var evidenceList = entity.Status == HypothesisStatus.Supported
                ? ParseEvidenceJson(entity.SupportingEvidenceJson)
                : ParseEvidenceJson(entity.RefutingEvidenceJson);

            evidenceList.Add(command.EvidenceText);
            var json = JsonSerializer.Serialize(evidenceList);

            if (entity.Status == HypothesisStatus.Supported)
                entity.SupportingEvidenceJson = json;
            else if (entity.Status == HypothesisStatus.Refuted)
                entity.RefutingEvidenceJson = json;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated hypothesis {HypothesisId} status to {Status}", id, command.Status);

        return ToResponse(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Hypotheses.FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Hypothesis), id);

        dbContext.Hypotheses.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted hypothesis {HypothesisId}", id);
    }

    public async Task<HypothesisPaperResponse> AddPaperAsync(Guid hypothesisId, AddHypothesisPaperCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hypothesis = await dbContext.Hypotheses
            .Include(h => h.HypothesisPapers)
            .FirstOrDefaultAsync(h => h.Id == hypothesisId, cancellationToken)
            ?? throw new NotFoundException(nameof(Hypothesis), hypothesisId);

        var existing = hypothesis.HypothesisPapers.FirstOrDefault(hp => hp.PaperId == command.PaperId && hp.EvidenceType == command.EvidenceType);
        if (existing != null)
            throw new ConflictException("Paper already added with this evidence type");

        var paper = await dbContext.Papers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == command.PaperId, cancellationToken);
        if (paper == null)
            throw new NotFoundException(nameof(Paper), command.PaperId);

        var hp = new HypothesisPaper
        {
            HypothesisId = hypothesisId,
            PaperId = command.PaperId,
            EvidenceType = command.EvidenceType,
            EvidenceText = command.EvidenceText,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.HypothesisPapers.Add(hp);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Added paper {PaperId} to hypothesis {HypothesisId} as {EvidenceType}",
            command.PaperId, hypothesisId, command.EvidenceType);

        return new HypothesisPaperResponse(
            hp.Id,
            command.PaperId,
            paper.Title,
            command.EvidenceType,
            command.EvidenceText);
    }

    public async Task DeletePaperAsync(Guid hypothesisId, Guid paperId, CancellationToken cancellationToken)
    {
        var hp = await dbContext.HypothesisPapers
            .FirstOrDefaultAsync(x => x.HypothesisId == hypothesisId && x.PaperId == paperId, cancellationToken);

        if (hp == null)
            throw new NotFoundException("HypothesisPaper", paperId);

        dbContext.HypothesisPapers.Remove(hp);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Removed paper {PaperId} from hypothesis {HypothesisId}", paperId, hypothesisId);
    }

    public async Task ExtractHypothesesFromAnalysisAsync(Guid analysisResultId, CancellationToken cancellationToken)
    {
        var analysisResult = await dbContext.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == analysisResultId, cancellationToken)
            ?? throw new NotFoundException(nameof(AnalysisResult), analysisResultId);

        if (string.IsNullOrEmpty(analysisResult.ResultJson))
        {
            logger.LogWarning("Analysis result {AnalysisResultId} has no result JSON", analysisResultId);
            return;
        }

        var resultNode = JsonNode.Parse(analysisResult.ResultJson);
        if (resultNode == null) return;

        var hypotheses = ExtractHypothesesFromJson(resultNode);
        var userId = GetUserIdFromAnalysis(analysisResult);

        foreach (var (title, description) in hypotheses)
        {
            var existing = await dbContext.Hypotheses
                .AnyAsync(h => h.UserId == userId && h.Title == title, cancellationToken);

            if (existing) continue;

            var entity = new Hypothesis
            {
                Title = title,
                Description = description,
                UserId = userId,
                Status = HypothesisStatus.Proposed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Hypotheses.Add(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Extracted {Count} hypotheses from analysis {AnalysisResultId}", hypotheses.Count, analysisResultId);
    }

    private static List<(string Title, string Description)> ExtractHypothesesFromJson(JsonNode node)
    {
        var hypotheses = new List<(string Title, string Description)>();

        if (node is JsonObject obj)
        {
            foreach (var prop in obj)
            {
                var key = prop.Key;
                var value = prop.Value;

                if (value is JsonObject nestedObj)
                {
                    if (nestedObj.ContainsKey("hypothesis") || nestedObj.ContainsKey("title"))
                    {
                        var title = nestedObj["hypothesis"]?.GetValue<string>() ?? nestedObj["title"]?.GetValue<string>() ?? key;
                        var description = nestedObj["description"]?.GetValue<string>() ?? nestedObj["evidence"]?.GetValue<string>() ?? "";
                        if (!string.IsNullOrWhiteSpace(title))
                            hypotheses.Add((title, description));
                    }

                    if (nestedObj.ContainsKey("contradictionHints") || nestedObj.ContainsKey("contradictions"))
                    {
                        var contradictions = nestedObj["contradictionHints"] ?? nestedObj["contradictions"];
                        if (contradictions is JsonArray arr)
                        {
                            foreach (var item in arr)
                            {
                                var text = item?.GetValue<string>();
                                if (!string.IsNullOrWhiteSpace(text))
                                    hypotheses.Add(($"Contradiction: {text}", "From contradiction hints"));
                            }
                        }
                    }

                    if (nestedObj.ContainsKey("noveltyHints") || nestedObj.ContainsKey("novelty"))
                    {
                        var novelties = nestedObj["noveltyHints"] ?? nestedObj["novelty"];
                        if (novelties is JsonArray arr)
                        {
                            foreach (var item in arr)
                            {
                                var text = item?.GetValue<string>();
                                if (!string.IsNullOrWhiteSpace(text))
                                    hypotheses.Add(($"Novel insight: {text}", "From novelty hints"));
                            }
                        }
                    }
                }
            }
        }

        return hypotheses;
    }

    private static Guid GetUserIdFromAnalysis(AnalysisResult analysisResult)
    {
        if (Guid.TryParse(analysisResult.CreatedBy, out var userId))
            return userId;
        return Guid.Empty;
    }

    private static List<string> ParseEvidenceJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static HypothesisResponse ToResponse(Hypothesis entity)
    {
        var supportingPapers = entity.HypothesisPapers
            .Where(hp => hp.EvidenceType == EvidenceType.Supporting)
            .Select(hp => new HypothesisPaperResponse(
                hp.Id,
                hp.PaperId,
                hp.Paper?.Title ?? string.Empty,
                hp.EvidenceType,
                hp.EvidenceText))
            .ToList();

        var refutingPapers = entity.HypothesisPapers
            .Where(hp => hp.EvidenceType == EvidenceType.Refuting)
            .Select(hp => new HypothesisPaperResponse(
                hp.Id,
                hp.PaperId,
                hp.Paper?.Title ?? string.Empty,
                hp.EvidenceType,
                hp.EvidenceText))
            .ToList();

        return new HypothesisResponse(
            entity.Id,
            entity.Title,
            entity.Description,
            entity.Status,
            entity.UserId,
            supportingPapers,
            refutingPapers,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
