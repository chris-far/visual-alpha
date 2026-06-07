using Microsoft.AspNetCore.Mvc;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(
    IReportConfigGenerator configGenerator,
    IPdfPreProcessor preProcessor,
    IColumnRangeResolver columnRangeResolver,
    IHoldingExtractor extractor,
    IReportConfigStore configStore) : ControllerBase
{
    [HttpPost("analyse")]
    public async Task<IActionResult> Analyse(IFormFile pdf, string reportId = "")
    {
        using var ms = new MemoryStream();
        await pdf.CopyToAsync(ms);
        var pdfBytes = ms.ToArray();

        using var configStream = new MemoryStream(pdfBytes);
        var draft =
            await configStore.GetAsync(reportId) ??
            await configGenerator.GenerateAsync(configStream);

        using var preProcessStream = new MemoryStream(pdfBytes);
        var structure = await preProcessor.AnalyseAsync(preProcessStream, draft);
        var report = columnRangeResolver.Resolve(draft, structure);
        await configStore.SaveAsync(report);

        using var extractStream = new MemoryStream(pdfBytes);
        var extractions = await extractor.ExtractAsync(extractStream, report);

        return Ok(new { report, extractions });
    }

    /// <summary>
    /// Save a reviewed ReportConfig.
    /// Step 2 — after the analyst has approved or adjusted the draft.
    /// </summary>
    [HttpPost("save-config")]
    public async Task<IActionResult> SaveConfig([FromBody] ReportConfig config)
    {
        var updated = config with { CreatedAt = config.CreatedAt == default ? DateTime.UtcNow : config.CreatedAt };
        await configStore.SaveAsync(updated);
        return Ok(new { saved = true, reportId = updated.ReportId });
    }

    /// <summary>Lists all saved report configs.</summary>
    [HttpGet("configs")]
    public async Task<IActionResult> GetAllConfigs()
    {
        var configs = await configStore.GetAllAsync();
        return Ok(configs);
    }

}
