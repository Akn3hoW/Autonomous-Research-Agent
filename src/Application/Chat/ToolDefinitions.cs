namespace AutonomousResearchAgent.Application.Chat;

public sealed record ToolDefinitions
{
    public static ToolDefinition ImportPapers => new(
        Name: "import_papers",
        Description: "Search for and import academic papers from external sources. Use this when the user wants to find and add new papers to the knowledge base.",
        Parameters: new ToolParameters(
            [
                new ToolParameter("query", "string", "The search query to find papers", true),
                new ToolParameter("maxResults", "integer", "Maximum number of papers to import (default: 5)", false)
            ])
    );

    public static ToolDefinition SearchPapers => new(
        Name: "search_papers",
        Description: "Search through already imported papers in the knowledge base. Use this when the user wants to find papers that are already in the system.",
        Parameters: new ToolParameters(
            [
                new ToolParameter("query", "string", "The search query", true),
                new ToolParameter("topK", "integer", "Maximum number of results to return (default: 10)", false)
            ])
    );

    public static ToolDefinition GetPaperDetails => new(
        Name: "get_paper_details",
        Description: "Retrieve full details about a specific paper including abstract, authors, and metadata.",
        Parameters: new ToolParameters(
            [
                new ToolParameter("paperId", "string", "The unique identifier of the paper (GUID format)", true)
            ])
    );

    public static ToolDefinition GenerateSummary => new(
        Name: "generate_summary",
        Description: "Create a summary of a paper. Use this when the user wants to generate or regenerate a summary for a specific paper.",
        Parameters: new ToolParameters(
            [
                new ToolParameter("paperId", "string", "The unique identifier of the paper (GUID format)", true),
                new ToolParameter("summaryType", "string", "Type of summary: 'brief', 'detailed', or 'critical' (default: 'brief')", false)
            ])
    );

    public static IReadOnlyList<ToolDefinition> All => [ImportPapers, SearchPapers, GetPaperDetails, GenerateSummary];
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    ToolParameters Parameters);

public sealed record ToolParameters(IReadOnlyList<ToolParameter> Required);

public sealed record ToolParameter(
    string Name,
    string Type,
    string Description,
    bool IsRequired);