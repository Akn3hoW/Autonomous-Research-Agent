namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class LocalEmbeddingOptions
{
    public const string SectionName = "LocalEmbedding";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
    public string EmbeddingsPath { get; set; } = "embeddings";
    public string ModelName { get; set; } = "Snowflake/snowflake-arctic-embed-m-v1.5";
    public int TimeoutSeconds { get; set; } = 60;
    public int VectorDimensions { get; set; } = 768;
    public bool AllowVariableDimensions { get; set; } = false;
}
