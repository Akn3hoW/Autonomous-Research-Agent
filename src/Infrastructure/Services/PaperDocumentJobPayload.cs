using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Infrastructure.Services;

public static class PaperDocumentJobPayload
{
    public static JsonObject Create(PaperDocument document)
    {
        return new JsonObject
        {
            ["paperId"] = document.PaperId,
            ["documentId"] = document.Id,
            ["sourceUrl"] = document.SourceUrl,
            ["requiresOcr"] = document.RequiresOcr,
            ["fileName"] = document.FileName,
            ["mediaType"] = document.MediaType
        };
    }
}
