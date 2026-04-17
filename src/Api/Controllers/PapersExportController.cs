using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Application.Export;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/papers")]
public sealed class PapersExportController(
    IPaperService paperService,
    IExportService exportService) : ControllerBase
{
    private const int MaxPages = 100;

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
        if (pageNumber < 1 || pageSize < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid pagination parameters",
                Detail = "Page number and page size must be positive."
            });
        }

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
        var maxPagesReached = false;

        while (true)
        {
            if (currentPage - pageNumber + 1 > MaxPages)
            {
                maxPagesReached = true;
                break;
            }

            var query = new PaperQuery(currentPage, pageSize);
            var result = await paperService.ListAsync(query, null, cancellationToken);

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

        if (maxPagesReached)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Page limit exceeded",
                Detail = $"Export limited to {MaxPages} pages. Use a smaller page size or narrower date range."
            });
        }

        var content = string.Join("\n\n", allBibtex);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/x-bibtex", "papers.bib");
    }

    private async Task<IActionResult> ExportAllPapersAsRis(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var allRis = new List<string>();
        var currentPage = pageNumber;
        var maxPagesReached = false;

        while (true)
        {
            if (currentPage - pageNumber + 1 > MaxPages)
            {
                maxPagesReached = true;
                break;
            }

            var query = new PaperQuery(currentPage, pageSize);
            var result = await paperService.ListAsync(query, null, cancellationToken);

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

        if (maxPagesReached)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Page limit exceeded",
                Detail = $"Export limited to {MaxPages} pages. Use a smaller page size or narrower date range."
            });
        }

        var content = string.Join("\n", allRis);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/x-research-info-systems", "papers.ris");
    }
}
