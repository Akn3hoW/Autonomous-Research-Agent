using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.Recommendations;

public interface IRecommendationService
{
    Task<PagedResult<PaperRecommendationModel>> GetRecommendationsAsync(RecommendationQuery query, CancellationToken cancellationToken = default);
}
