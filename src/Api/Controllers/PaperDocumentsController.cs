using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Documents;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route("api/v1/papers/{paperId:guid}/documents")]
public sealed class PaperDocumentsController(IPaperDocumentService paperDocumentService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaperDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PaperDocumentDto>>> GetDocuments(Guid paperId, CancellationToken cancellationToken)
    {
        var documents = await paperDocumentService.ListByPaperIdAsync(paperId, cancellationToken);
        return Ok(documents.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{documentId:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PaperDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaperDocumentDto>> GetDocument(Guid paperId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await paperDocumentService.GetByIdAsync(paperId, documentId, cancellationToken);
        return Ok(document.ToDto());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(PaperDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaperDocumentDto>> CreateDocument(Guid paperId, [FromBody] CreatePaperDocumentRequest request, CancellationToken cancellationToken)
    {
        var created = await paperDocumentService.CreateAsync(request.ToApplicationModel(paperId), cancellationToken);
        return CreatedAtAction(nameof(GetDocument), new { paperId, documentId = created.Id }, created.ToDto());
    }

    [HttpPost("{documentId:guid}/queue-processing")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(PaperDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaperDocumentDto>> QueueProcessing(Guid paperId, Guid documentId, [FromBody] QueuePaperDocumentProcessingRequest request, CancellationToken cancellationToken)
    {
        var updated = await paperDocumentService.QueueProcessingAsync(paperId, documentId, request.ToApplicationModel(User.GetActorName()), cancellationToken);
        return Ok(updated.ToDto());
    }
}
