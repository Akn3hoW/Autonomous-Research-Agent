using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.Search;

public interface ISearchService
{
    Task<PagedResult<SearchResultModel>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken);
    Task<PagedResult<SearchResultModel>> SemanticSearchAsync(SemanticSearchRequestModel request, CancellationToken cancellationToken);
    Task<PagedResult<SearchResultModel>> HybridSearchAsync(HybridSearchRequestModel request, CancellationToken cancellationToken);
    Task<PagedResult<ChunkSearchResultModel>> SearchDocumentChunksAsync(ChunkSearchRequestModel request, CancellationToken cancellationToken);
}
