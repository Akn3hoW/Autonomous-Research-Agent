using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Application.Export;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route("api/v1/papers")]
public sealed class PapersExportController(
    IPaperService paperService,
    IExportService exportService) : ControllerBase
{
    [HttpGet("export")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportPapers(
        [FromQuery] string format,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(format, "bibtex", StringComparison.OrdinalIgnoreCase))
        {
            return await ExportAllPapersAsBibtex(pageNumber, pageSize, cancellationToken);
        }

        if (string.Equals(format, "ris", StringComparison.OrdinalIgnoreCase))
        {
            return await ExportAllPapersAsRis(pageNumber, pageSize, cancellationToken);
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Invalid format",
            Detail = "Supported formats are 'bibtex' and 'ris'."
        });
    }

    private async Task<IActionResult> ExportAllPapersAsBibtex(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var allBibtex = new List<string>();
        var currentPage = pageNumber;

        while (true)
        {
            var query = new PaperQuery(currentPage, pageSize);
            var result = await paperService.ListAsync(query, cancellationToken);

            foreach (var paper in result.Items)
            {
                var bibtex = exportService.ToBibtex(
                    paper.Id,
                    paper.Title,
                    paper.Authors,
                    paper.Year,
                    null,
                    paper.Venue,
                    paper.CitationCount);
                allBibtex.Add(bibtex);
            }

            if (currentPage >= result.TotalPages)
                break;

            currentPage++;
        }

        var content = string.Join("\n\n", allBibtex);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/x-bibtex", "papers.bib");
    }

    private async Task<IActionResult> ExportAllPapersAsRis(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var allRis = new List<string>();
        var currentPage = pageNumber;

        while (true)
        {
            var query = new PaperQuery(currentPage, pageSize);
            var result = await paperService.ListAsync(query, cancellationToken);

            foreach (var paper in result.Items)
            {
                var ris = exportService.ToRis(
                    paper.Id,
                    paper.Title,
                    paper.Authors,
                    paper.Year,
                    null,
                    paper.Venue,
                    paper.CitationCount);
                allRis.Add(ris);
            }

            if (currentPage >= result.TotalPages)
                break;

            currentPage++;
        }

        var content = string.Join("\n", allRis);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/x-research-info-systems", "papers.ris");
    }
}
