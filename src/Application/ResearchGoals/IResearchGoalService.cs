using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.ResearchGoals;

public interface IResearchGoalService
{
    Task<ResearchGoalModel> CreateResearchGoalAsync(CreateResearchGoalCommand command, CancellationToken cancellationToken);
    Task<ResearchGoalModel> GetResearchGoalStatusAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IEnumerable<ResearchGoalTemplateModel>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task<ResearchGoalModel> CreateFromTemplateAsync(CreateFromTemplateCommand command, CancellationToken cancellationToken);
}

public sealed record CreateResearchGoalCommand(
    string Goal,
    int MaxPapers,
    string? Field,
    string? CreatedBy);

public sealed record ResearchGoalModel(
    Guid JobId,
    string Status,
    List<ResearchGoalStepModel> Steps,
    string? ResultJson);

public sealed record ResearchGoalStepModel(
    string StepType,
    string Description,
    Guid? SubJobId,
    string Status);

public sealed record ResearchGoalTemplateModel(
    Guid Id,
    string Name,
    string? Description,
    string GoalType,
    string? Parameters,
    string PromptTemplate);

public sealed record CreateFromTemplateCommand(
    Guid TemplateId,
    string Name,
    Dictionary<string, string> Parameters,
    string? CreatedBy);