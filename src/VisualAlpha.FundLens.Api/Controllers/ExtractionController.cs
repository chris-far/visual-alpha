using Microsoft.AspNetCore.Mvc;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Api.Controllers;

[ApiController]
[Route("api/extract")]
public sealed class ExtractionController(
    IHoldingExtractor extractor,
    IReportConfigStore configStore,
    IValidationRunner validator) : ControllerBase
{
    /// <summary>
    /// Extract holdings from a PDF using the saved config for the given fundId.
    /// Returns extracted holdings + validation report.
    /// </summary>
    [HttpPost("{reportId}")]
    public async Task<IActionResult> Extract(string reportId, IFormFile pdf)
    {
        var report = await configStore.GetAsync(reportId);
        if (report is null) return NotFound(new { error = $"No config found for reportId: {reportId}" });

        await using var stream = pdf.OpenReadStream();
        var results = await extractor.ExtractAsync(stream, report);
        var validations = await Task.WhenAll(results.Select(validator.RunAsync));

        return Ok(results.Zip(validations, (r, v) => new { extraction = r, validation = v }));
    }
}
