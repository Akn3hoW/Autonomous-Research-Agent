namespace AutonomousResearchAgent.Infrastructure.Services;

/// <summary>
/// Configuration options for paper document processing operations including download, OCR, and storage.
/// </summary>
public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    /// <summary>
    /// Root directory path for storing downloaded paper documents.
    /// </summary>
    public string StorageRoot { get; set; } = "data/paper-documents";

    /// <summary>
    /// Maximum allowed document download size in megabytes. Documents exceeding this size will be rejected.
    /// </summary>
    public int MaxDownloadSizeMegabytes { get; set; } = 50;

    /// <summary>
    /// Timeout for document downloads in seconds. Downloads exceeding this timeout will be cancelled.
    /// Default: 300 seconds (5 minutes). For large files on slow connections, consider increasing this value.
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Path to the OCR executable (e.g., ocrmypdf). Must be available on the system PATH or as an absolute path.
    /// </summary>
    public string OcrExecutablePath { get; set; } = "ocrmypdf";

    /// <summary>
    /// Minimum character count below which OCR fallback will be triggered. If extracted text is shorter
    /// than this threshold, the document will be reprocessed using OCR.
    /// </summary>
    public int OcrFallbackMinimumCharacters { get; set; } = 32;
}
