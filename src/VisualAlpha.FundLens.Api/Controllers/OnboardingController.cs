using Microsoft.AspNetCore.Mvc;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(
    IReportOnboardingService onboardingService,
    IReportConfigStore configStore,
    IValidationRunner validator) : ControllerBase
{
    [HttpPost("onboard")]
    public async Task<IActionResult> Onboard(IFormFile pdf)
    {
        await using var stream = pdf.OpenReadStream();
        var result = await onboardingService.OnboardAsync(stream);
        var validations = await Task.WhenAll(result.Extractions.Select(validator.RunAsync));
        var paired = result.Extractions.Zip(validations, (e, v) => new { extraction = e, validation = v });
        return Ok(new { report = result.Report, extractions = paired });
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
