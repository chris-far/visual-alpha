using Microsoft.AspNetCore.Mvc;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(
    IReportOnboardingService onboardingService,
    IReportConfigStore configStore) : ControllerBase
{
    [HttpPost("onboard")]
    public async Task<IActionResult> Onboard(IFormFile pdf)
    {
        await using var stream = pdf.OpenReadStream();
        var result = await onboardingService.OnboardAsync(stream);
        return Ok(new { report = result.Report, extractions = result.Extractions });
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

}
