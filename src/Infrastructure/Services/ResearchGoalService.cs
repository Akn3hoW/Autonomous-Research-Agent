using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.ResearchGoals;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class ResearchGoalService(
    ApplicationDbContext dbContext,
    IJobService jobService,
    ILogger<ResearchGoalService> logger) : IResearchGoalService
{
    private readonly OpenRouterOptions _openRouterOptions = new();

    public async Task<ResearchGoalModel> CreateResearchGoalAsync(CreateResearchGoalCommand command, CancellationToken cancellationToken)
    {
        var rootPayload = new JsonObject
        {
            ["goal"] = command.Goal,
            ["maxPapers"] = command.MaxPapers,
            ["field"] = command.Field ?? string.Empty
        };

        var rootJob = await jobService.CreateAsync(
            new CreateJobCommand(JobType.ResearchGoal, rootPayload, null, command.CreatedBy),
            cancellationToken);

        var steps = new List<ResearchGoalStepModel>();
        var searchJob = await CreateChildJobAsync(rootJob.Id, JobType.SearchPapers,
            new JsonObject { ["goal"] = command.Goal, ["maxPapers"] = command.MaxPapers, ["field"] = command.Field ?? string.Empty },
            cancellationToken);
        steps.Add(new ResearchGoalStepModel("SearchPapers", $"Searching Semantic Scholar for: {command.Goal}", searchJob.Id, searchJob.Status.ToString()));

        var importJob = await CreateChildJobAsync(rootJob.Id, JobType.ImportPaper,
            new JsonObject { ["searchJobId"] = searchJob.Id.ToString() },
            cancellationToken);
        steps.Add(new ResearchGoalStepModel("ImportPaper", "Importing papers from search results", importJob.Id, importJob.Status.ToString()));

        var summarizeJob = await CreateChildJobAsync(rootJob.Id, JobType.SummarizePaperChild,
            new JsonObject { ["importJobId"] = importJob.Id.ToString() },
            cancellationToken);
        steps.Add(new ResearchGoalStepModel("SummarizePaper", "Generating summaries for papers", summarizeJob.Id, summarizeJob.Status.ToString()));

        var analyzeJob = await CreateChildJobAsync(rootJob.Id, JobType.AnalyzePaper,
            new JsonObject { ["summarizeJobId"] = summarizeJob.Id.ToString() },
            cancellationToken);
        steps.Add(new ResearchGoalStepModel("AnalyzePaper", "Analyzing paper summaries and content", analyzeJob.Id, analyzeJob.Status.ToString()));

        var reportJob = await CreateChildJobAsync(rootJob.Id, JobType.GenerateReport,
            new JsonObject { ["analyzeJobId"] = analyzeJob.Id.ToString(), ["goal"] = command.Goal },
            cancellationToken);
        steps.Add(new ResearchGoalStepModel("GenerateReport", "Generating structured research report", reportJob.Id, reportJob.Status.ToString()));

        return new ResearchGoalModel(rootJob.Id, rootJob.Status.ToString(), steps, null);
    }

    public async Task<ResearchGoalModel> GetResearchGoalStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs.AsNoTracking()
            .Include(j => j.ChildJobs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), jobId);

        var steps = job.ChildJobs
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ResearchGoalStepModel(
                c.Type.ToString(),
                GetStepDescription(c.Type),
                c.Id,
                c.Status.ToString()))
            .ToList();

        return new ResearchGoalModel(job.Id, job.Status.ToString(), steps, job.ResultJson);
    }

    private async Task<JobModel> CreateChildJobAsync(Guid parentJobId, JobType type, JsonObject payload, CancellationToken cancellationToken)
    {
        var command = new CreateJobCommand(type, payload, null, null);
        var entity = new Job
        {
            Type = type,
            Status = JobStatus.Queued,
            PayloadJson = JsonNodeMapper.Serialize(payload) ?? "{}",
            ParentJobId = parentJobId
        };

        dbContext.Jobs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created child job {JobId} of type {JobType} for parent {ParentJobId}", entity.Id, type, parentJobId);
        return entity.ToModel();
    }

    private static string GetStepDescription(JobType type) => type switch
    {
        JobType.SearchPapers => "Searching Semantic Scholar",
        JobType.ImportPaper => "Importing papers",
        JobType.SummarizePaperChild => "Summarizing papers",
        JobType.AnalyzePaper => "Analyzing papers",
        JobType.GenerateReport => "Generating research report",
        _ => type.ToString()
    };
}