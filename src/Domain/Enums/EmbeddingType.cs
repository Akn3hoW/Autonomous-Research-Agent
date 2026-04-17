namespace AutonomousResearchAgent.Domain.Enums;

public enum EmbeddingType
{
    PaperAbstract = 0,
    PaperMetadata = 1,
    Summary = 2,
    PaperSummary = Summary,
    Analysis = 3,
    Query = 4,
    DocumentChunk = 5,
    Paper = 6
}
