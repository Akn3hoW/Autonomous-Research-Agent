using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AutonomousResearchAgent.Application.Collections;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class CollectionService : ICollectionService
{
    private readonly ApplicationDbContext _db;

    public CollectionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<CollectionListItem>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var collections = await _db.Collections
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CollectionListItem(
                c.Id,
                c.Name,
                c.Description,
                c.IsShared,
                c.CollectionPapers.Count,
                c.SortOrder,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(cancellationToken);

        return collections;
    }

    public async Task<CollectionDetail> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .Include(c => c.CollectionPapers)
            .ThenInclude(cp => cp.Paper)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", id);

        var papers = collection.CollectionPapers
            .OrderBy(cp => cp.SortOrder)
            .Select(cp => new CollectionPaperDetail(
                cp.PaperId,
                cp.Paper.Title,
                cp.Paper.Authors,
                cp.Paper.Year,
                cp.SortOrder,
                cp.AddedAt))
            .ToList();

        var shareUrl = collection.ShareToken is not null
            ? $"/api/v1/collections/shared/{collection.ShareToken}"
            : null;

        return new CollectionDetail(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.IsShared,
            collection.IsPublic,
            collection.ShareToken,
            shareUrl,
            collection.SortOrder,
            collection.CreatedAt,
            collection.UpdatedAt,
            papers);
    }

    public async Task<CollectionListItem> CreateAsync(CreateCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var maxSortOrder = await _db.Collections
            .Where(c => c.UserId == command.UserId)
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;

        var collection = new Collection
        {
            UserId = command.UserId,
            Name = command.Name,
            Description = command.Description,
            IsShared = command.IsShared,
            SortOrder = maxSortOrder + 1
        };

        _db.Collections.Add(collection);
        await _db.SaveChangesAsync(cancellationToken);

        return new CollectionListItem(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.IsShared,
            0,
            collection.SortOrder,
            collection.CreatedAt,
            collection.UpdatedAt);
    }

    public async Task<CollectionListItem> UpdateAsync(Guid id, UpdateCollectionCommand command, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", id);

        if (command.Name is not null)
            collection.Name = command.Name;
        if (command.Description is not null)
            collection.Description = command.Description;
        if (command.IsShared.HasValue)
            collection.IsShared = command.IsShared.Value;

        await _db.SaveChangesAsync(cancellationToken);

        var paperCount = await _db.CollectionPapers.CountAsync(cp => cp.CollectionId == id, cancellationToken);

        return new CollectionListItem(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.IsShared,
            paperCount,
            collection.SortOrder,
            collection.CreatedAt,
            collection.UpdatedAt);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", id);

        _db.Collections.Remove(collection);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPaperAsync(Guid collectionId, AddPaperCommand command, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var collection = await _db.Collections
                .Include(c => c.CollectionPapers)
                .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Collection", collectionId);

            var paperExists = await _db.Papers.AnyAsync(p => p.Id == command.PaperId, cancellationToken);
            if (!paperExists)
                throw new NotFoundException("Paper", command.PaperId);

            if (collection.CollectionPapers.Any(cp => cp.PaperId == command.PaperId))
                throw new ConflictException($"Paper '{command.PaperId}' already in collection");

            var maxSortOrder = collection.CollectionPapers.Any()
                ? collection.CollectionPapers.Max(cp => cp.SortOrder)
                : 0;

            var collectionPaper = new CollectionPaper
            {
                CollectionId = collection.Id,
                PaperId = command.PaperId,
                SortOrder = maxSortOrder + 1,
                AddedAt = DateTimeOffset.UtcNow
            };

            _db.CollectionPapers.Add(collectionPaper);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RemovePaperAsync(Guid collectionId, RemovePaperCommand command, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", collectionId);

        var collectionPaper = await _db.CollectionPapers
            .FirstOrDefaultAsync(cp => cp.CollectionId == collectionId && cp.PaperId == command.PaperId, cancellationToken)
            ?? throw new NotFoundException("Paper", command.PaperId);

        _db.CollectionPapers.Remove(collectionPaper);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderPapersAsync(Guid collectionId, ReorderPapersCommand command, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var collection = await _db.Collections
            .Include(c => c.CollectionPapers)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", collectionId);

        var paperIds = command.PaperIds.ToList();
        foreach (var cp in collection.CollectionPapers)
        {
            var index = paperIds.IndexOf(cp.PaperId);
            if (index >= 0)
                cp.SortOrder = index;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]> ExportAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .Include(c => c.CollectionPapers)
            .ThenInclude(cp => cp.Paper)
            .ThenInclude(p => p.Summaries)
            .Include(c => c.CollectionPapers)
            .ThenInclude(cp => cp.Paper)
            .ThenInclude(p => p.Documents)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", collectionId);

        using var memoryStream = new MemoryStream();
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true);

        var manifest = new CollectionManifest
        {
            CollectionId = collection.Id,
            CollectionName = collection.Name,
            ExportedAt = DateTimeOffset.UtcNow,
            Papers = []
        };

        foreach (var cp in collection.CollectionPapers.OrderBy(cp => cp.SortOrder))
        {
            var paper = cp.Paper;
            var safeName = SanitizeFileName(paper.Title);

            var latestSummary = paper.Summaries
                .Where(s => s.Status == Domain.Enums.SummaryStatus.Approved)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefault();

            if (latestSummary?.SummaryJson is not null)
            {
                var summaryEntry = archive.CreateEntry($"{safeName}/summary.md");
                using (var writer = new StreamWriter(summaryEntry.Open()))
                {
                    await writer.WriteAsync($"# {paper.Title}\n\n");
                    if (paper.Authors.Any())
                        await writer.WriteAsync($"**Authors:** {string.Join(", ", paper.Authors)}\n\n");
                    if (paper.Year.HasValue)
                        await writer.WriteAsync($"**Year:** {paper.Year}\n\n");
                    await writer.WriteAsync(latestSummary.SummaryJson);
                }

                manifest.Papers.Add(new ManifestPaper
                {
                    PaperId = paper.Id,
                    Title = paper.Title,
                    HasSummary = true,
                    HasExtractedText = false
                });
            }

            var extractedText = paper.Documents
                .Where(d => d.ExtractedText is not null)
                .OrderByDescending(d => d.UpdatedAt)
                .FirstOrDefault()?.ExtractedText;

            if (!string.IsNullOrEmpty(extractedText))
            {
                var textEntry = archive.CreateEntry($"{safeName}/extracted_text.txt");
                using var textWriter = new StreamWriter(textEntry.Open());
                await textWriter.WriteAsync(extractedText);

                var manifestPaper = manifest.Papers.FirstOrDefault(p => p.PaperId == paper.Id);
                if (manifestPaper is not null)
                    manifestPaper.HasExtractedText = true;
            }
        }

        var manifestEntry = archive.CreateEntry("collection_manifest.json");
        using (var manifestWriter = new StreamWriter(manifestEntry.Open()))
        {
            await manifestWriter.WriteAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        return memoryStream.ToArray();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 100 ? sanitized[..100] : sanitized;
    }

    public async Task<SharedCollectionDetail?> GetSharedCollectionAsync(string token, CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .Include(c => c.CollectionPapers)
            .ThenInclude(cp => cp.Paper)
            .FirstOrDefaultAsync(c => c.ShareToken == token && c.IsPublic, cancellationToken);

        if (collection is null)
            return null;

        var papers = collection.CollectionPapers
            .OrderBy(cp => cp.SortOrder)
            .Select(cp => new SharedCollectionPaperDetail(
                cp.PaperId,
                cp.Paper.Title,
                cp.Paper.Authors,
                cp.Paper.Year,
                cp.SortOrder,
                cp.AddedAt))
            .ToList();

        return new SharedCollectionDetail(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.SortOrder,
            collection.CreatedAt,
            collection.UpdatedAt,
            papers);
    }

    public async Task<string> GenerateShareTokenAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", collectionId);

        if (string.IsNullOrEmpty(collection.ShareToken))
        {
            collection.ShareToken = GenerateToken();
        }

        collection.IsPublic = true;
        await _db.SaveChangesAsync(cancellationToken);

        return collection.ShareToken;
    }

    public async Task RevokeShareTokenAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken)
    {
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Collection", collectionId);

        collection.ShareToken = null;
        collection.IsPublic = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateToken()
    {
        var bytes = new byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private class CollectionManifest
    {
        public Guid CollectionId { get; init; }
        public string CollectionName { get; init; } = string.Empty;
        public DateTimeOffset ExportedAt { get; init; }
        public List<ManifestPaper> Papers { get; init; } = [];
    }

    private class ManifestPaper
    {
        public Guid PaperId { get; init; }
        public string Title { get; init; } = string.Empty;
        public bool HasSummary { get; init; }
        public bool HasExtractedText { get; set; }
    }
}