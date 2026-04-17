namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public string StorageRoot { get; set; } = "data/paper-documents";

    public int MaxDownloadSizeMegabytes { get; set; } = 50;

    public int DownloadTimeoutSeconds { get; set; } = 300;

    public string OcrExecutablePath { get; set; } = "ocrmypdf";

    public int OcrFallbackMinimumCharacters { get; set; } = 32;

    public bool UseVisionFallback { get; set; } = false;

    public string VisionModel { get; set; } = "gemini-1.5-flash";
}