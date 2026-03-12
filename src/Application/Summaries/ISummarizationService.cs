using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Papers;

namespace AutonomousResearchAgent.Application.Summaries;

public interface ISummarizationService
{
    Task<JsonNode?> GenerateSummaryAsync(PaperDetail paper, string modelName, string promptVersion, CancellationToken cancellationToken);
}
