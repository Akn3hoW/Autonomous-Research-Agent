using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.ResearchGoals;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.ResearchGoals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/research-goals")]
public sealed class ResearchGoalsController(IResearchGoalService researchGoalService) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(ResearchGoalResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ResearchGoalResponse>> CreateResearchGoal(
        [FromBody] CreateResearchGoalRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateResearchGoalCommand(
            request.Goal,
            request.MaxPapers,
            request.Field,
            User.GetActorName());

        var result = await researchGoalService.CreateResearchGoalAsync(command, cancellationToken);

        var response = new ResearchGoalResponse(
            result.JobId,
            result.Status,
            result.Steps.Select(s => new ResearchGoalStep(s.StepType, s.Description, s.SubJobId, s.Status)).ToList());

        return CreatedAtAction(nameof(GetResearchGoal), new { id = result.JobId }, response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(ResearchGoalResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResearchGoalResponse>> GetResearchGoal(Guid id, CancellationToken cancellationToken)
    {
        var result = await researchGoalService.GetResearchGoalStatusAsync(id, cancellationToken);

        var response = new ResearchGoalResponse(
            result.JobId,
            result.Status,
            result.Steps.Select(s => new ResearchGoalStep(s.StepType, s.Description, s.SubJobId, s.Status)).ToList());

        return Ok(response);
    }

    [HttpGet("templates")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IEnumerable<ResearchGoalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ResearchGoalTemplateDto>>> GetTemplates(CancellationToken cancellationToken)
    {
        var templates = await researchGoalService.GetTemplatesAsync(cancellationToken);
        var dtos = templates.Select(t => new ResearchGoalTemplateDto(
            t.Id,
            t.Name,
            t.Description,
            t.GoalType,
            t.Parameters,
            t.PromptTemplate));
        return Ok(dtos);
    }

    [HttpPost("from-template")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(ResearchGoalResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ResearchGoalResponse>> CreateFromTemplate(
        [FromBody] CreateFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateFromTemplateCommand(
            request.TemplateId,
            request.Name,
            request.Parameters,
            User.GetActorName());

        var result = await researchGoalService.CreateFromTemplateAsync(command, cancellationToken);

        var response = new ResearchGoalResponse(
            result.JobId,
            result.Status,
            result.Steps.Select(s => new ResearchGoalStep(s.StepType, s.Description, s.SubJobId, s.Status)).ToList());

        return CreatedAtAction(nameof(GetResearchGoal), new { id = result.JobId }, response);
    }
}