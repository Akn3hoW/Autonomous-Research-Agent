using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PaperDocumentJobPayloadTests
{
    [Fact]
    public void Create_returns_json_with_all_required_fields()
    {
        var document = new PaperDocument
        {
            Id = Guid.NewGuid(),
            PaperId = Guid.NewGuid(),
            SourceUrl = "https://example.org/paper.pdf",
            RequiresOcr = true,
            FileName = "paper.pdf",
            MediaType = "application/pdf"
        };

        var result = PaperDocumentJobPayload.Create(document);

        Assert.Equal(document.PaperId, result["paperId"]?.GetValue<Guid>());
        Assert.Equal(document.Id, result["documentId"]?.GetValue<Guid>());
        Assert.Equal(document.SourceUrl, result["sourceUrl"]?.GetValue<string>());
        Assert.Equal(document.RequiresOcr, result["requiresOcr"]?.GetValue<bool>());
        Assert.Equal(document.FileName, result["fileName"]?.GetValue<string>());
        Assert.Equal(document.MediaType, result["mediaType"]?.GetValue<string>());
    }

    [Fact]
    public void Create_handles_null_optional_fields()
    {
        var document = new PaperDocument
        {
            Id = Guid.NewGuid(),
            PaperId = Guid.NewGuid(),
            SourceUrl = "https://example.org/paper.pdf",
            RequiresOcr = false,
            FileName = null,
            MediaType = null
        };

        var result = PaperDocumentJobPayload.Create(document);

        Assert.Equal(document.PaperId, result["paperId"]?.GetValue<Guid>());
        Assert.Null(result["fileName"]?.GetValue<string>());
        Assert.Null(result["mediaType"]?.GetValue<string>());
    }

    [Fact]
    public void Create_returns_valid_json_object()
    {
        var document = new PaperDocument
        {
            Id = Guid.NewGuid(),
            PaperId = Guid.NewGuid(),
            SourceUrl = "https://example.org/paper.pdf"
        };

        var result = PaperDocumentJobPayload.Create(document);

        Assert.IsType<JsonObject>(result);
    }
}
