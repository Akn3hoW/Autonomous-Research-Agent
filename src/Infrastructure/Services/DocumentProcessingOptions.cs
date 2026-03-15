namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public string StorageRoot { get; set; } = "data/paper-documents";
    public int MaxDownloadSizeMegabytes { get; set; } = 50;
}
