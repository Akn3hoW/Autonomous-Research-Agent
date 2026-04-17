using System.Text;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Chat;
using AutonomousResearchAgent.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/chat")]
public sealed class ChatController(IChatService chatService, ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Response.ContentType = "text/event-stream";
        Response.StatusCode = 200;

        try
        {
            await foreach (var token in chatService.StreamChatAsync(request.Question, request.TopK, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;
                var bytes = Encoding.UTF8.GetBytes($"data: {token}\n\n");
                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            var endBytes = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await Response.Body.WriteAsync(endBytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in streaming chat");
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
            }
        }
    }

    [HttpPost("with-tools")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task ChatWithTools([FromBody] ChatRequestWithToolsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Response.ContentType = "text/event-stream";
        Response.StatusCode = 200;

        try
        {
            var appRequest = new ChatRequestWithTools(request.Question, request.TopK, request.IncludeTools);
            await foreach (var token in chatService.StreamChatWithToolsAsync(request.Question, request.TopK, request.IncludeTools, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;
                var bytes = Encoding.UTF8.GetBytes($"data: {token}\n\n");
                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            var endBytes = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await Response.Body.WriteAsync(endBytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in streaming chat with tools");
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
            }
        }
    }

    [HttpPost("sources")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponseDto>> ChatWithSources([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await chatService.ChatAsync(request.Question, request.TopK, cancellationToken);

        var sources = result.Sources.Select(s => new ChatSourceDto(
            s.PaperId,
            s.PaperTitle,
            s.ChunkId,
            s.ChunkText,
            s.Score)).ToArray();

        return Ok(new ChatResponseDto(result.Answer, sources));
    }

    [HttpGet("sources/{chunkId:guid}/{paperId:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(SourceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SourceDetailDto>> GetSource(Guid chunkId, Guid paperId, CancellationToken cancellationToken)
    {
        var citation = await chatService.GetSourceAsync(chunkId, paperId, cancellationToken);

        if (citation == null)
        {
            return NotFound();
        }

        return Ok(new SourceDetailDto(
            citation.PaperId,
            citation.ChunkId,
            citation.ChunkText,
            citation.Score,
            citation.Position,
            string.Empty));
    }
}