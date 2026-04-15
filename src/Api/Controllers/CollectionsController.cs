using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Collections;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Collections;
using AutonomousResearchAgent.Application.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/collections")]
public sealed class CollectionsController(
    ICollectionService collectionService,
    IExportService exportService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CollectionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<CollectionResponse>>> GetCollections(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var collections = await collectionService.ListAsync(userId, cancellationToken);
        return Ok(collections.Select(c => new CollectionResponse(
            c.Id, c.Name, c.Description, c.IsShared, c.PaperCount, c.SortOrder, c.CreatedAt, c.UpdatedAt)).ToList());
    }

    [HttpGet("shared/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SharedCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SharedCollectionResponse>> GetSharedCollection(string token, CancellationToken cancellationToken)
    {
        var collection = await collectionService.GetSharedCollectionAsync(token, cancellationToken);
        if (collection is null)
            return NotFound();
        return Ok(new SharedCollectionResponse(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.SortOrder,
            collection.CreatedAt,
            collection.UpdatedAt,
            collection.Papers.Select(p => new SharedCollectionPaperItem(
                p.PaperId, p.Title, p.Authors, p.Year, p.SortOrder, p.AddedAt)).ToList()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(CollectionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionDetailResponse>> GetCollection(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var collection = await collectionService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(new CollectionDetailResponse(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.IsShared,
            collection.IsPublic,
            collection.ShareToken,
            collection.ShareUrl,
            collection.SortOrder,
            collection.CreatedAt,
            collection.UpdatedAt,
            collection.Papers.Select(p => new CollectionPaperItem(
                p.PaperId, p.Title, p.Authors, p.Year, p.SortOrder, p.AddedAt)).ToList()));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CollectionResponse>> CreateCollection([FromBody] CreateCollectionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new CreateCollectionCommand(userId, request.Name, request.Description, request.IsShared);
        var created = await collectionService.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetCollection), new { id = created.Id }, new CollectionResponse(
            created.Id, created.Name, created.Description, created.IsShared, created.PaperCount, created.SortOrder, created.CreatedAt, created.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CollectionResponse>> UpdateCollection(Guid id, [FromBody] UpdateCollectionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new UpdateCollectionCommand(request.Name, request.Description, request.IsShared);
        var updated = await collectionService.UpdateAsync(id, command, userId, cancellationToken);
        return Ok(new CollectionResponse(
            updated.Id, updated.Name, updated.Description, updated.IsShared, updated.PaperCount, updated.SortOrder, updated.CreatedAt, updated.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCollection(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await collectionService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/papers/{paperId:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddPaper(Guid id, Guid paperId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new AddPaperCommand(paperId);
        await collectionService.AddPaperAsync(id, command, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/papers/{paperId:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePaper(Guid id, Guid paperId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new RemovePaperCommand(paperId);
        await collectionService.RemovePaperAsync(id, command, userId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/papers/reorder")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderPapers(Guid id, [FromBody] ReorderPapersRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new ReorderPapersCommand(request.PaperIds);
        await collectionService.ReorderPapersAsync(id, command, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/export")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportCollection(Guid id, [FromQuery] string format, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var collection = await collectionService.GetByIdAsync(id, userId, cancellationToken);

        if (string.Equals(format, "bibtex", StringComparison.OrdinalIgnoreCase))
        {
            var bibtexEntries = collection.Papers.Select(p =>
                exportService.ToBibtex(p.PaperId, p.Title, p.Authors, p.Year, null, null, 0));
            var content = string.Join("\n\n", bibtexEntries);
            return File(System.Text.Encoding.UTF8.GetBytes(content), "application/x-bibtex", $"collection_{id}.bib");
        }

        var zipBytes = await collectionService.ExportAsync(id, userId, cancellationToken);
        return File(zipBytes, "application/zip", $"collection_{id}.zip");
    }

    [HttpPost("{id:guid}/share")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(ShareCollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareCollectionResponse>> ShareCollection(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var token = await collectionService.GenerateShareTokenAsync(id, userId, cancellationToken);
        var shareUrl = $"/{ApiConstants.ApiPrefix}/collections/shared/{token}";
        return Ok(new ShareCollectionResponse(token, shareUrl));
    }

    [HttpDelete("{id:guid}/share")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSharing(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await collectionService.RevokeShareTokenAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private int? GetUserId() => User.GetUserId();
}