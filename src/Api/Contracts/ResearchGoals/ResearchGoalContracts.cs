using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.ResearchGoals;

public sealed class CreateResearchGoalRequest
{
    public string Goal { get; init; } = string.Empty;
    public int MaxPapers { get; init; } = 20;
    public string? Field { get; init; }
}

public sealed record ResearchGoalResponse(
    Guid JobId,
    string Status,
    List<ResearchGoalStep> Steps);

public sealed record ResearchGoalStep(
    string StepType,
    string Description,
    Guid? SubJobId,
    string Status);

public sealed record ResearchGoalTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string GoalType,
    string? Parameters,
    string PromptTemplate);

public sealed class CreateFromTemplateRequest
{
    public Guid TemplateId { get; init; }
    public string Name { get; init; } = string.Empty;
    public Dictionary<string, string> Parameters { get; init; } = new();
}