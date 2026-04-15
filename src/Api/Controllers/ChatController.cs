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
        Response.ContentType = "text/plain; charset=utf-8";
        Response.StatusCode = 200;

        try
        {
            await foreach (var token in chatService.StreamChatAsync(request.Question, request.TopK, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;
                var bytes = Encoding.UTF8.GetBytes(token);
                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
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

    [HttpPost("sources")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponseDto>> ChatWithSources([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var result = await chatService.ChatAsync(request.Question, request.TopK, cancellationToken);

        var sources = result.Sources.Select(s => new ChatSourceDto(
            s.PaperId,
            s.PaperTitle,
            s.ChunkId,
            s.ChunkText,
            s.Score)).ToArray();

        return Ok(new ChatResponseDto(result.Answer, sources));
    }
}
