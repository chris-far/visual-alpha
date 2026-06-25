using Microsoft.AspNetCore.Mvc;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Api.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class DashboardController(IReportConfigStore configStore) : ControllerBase
{
[HttpGet("reports")]
    public async Task<IActionResult> GetAllReports() =>
        Ok(await configStore.GetAllAsync());

    [HttpDelete("reports/{reportId}")]
    public async Task<IActionResult> DeleteReport(string reportId)
    {
        var deleted = await configStore.DeleteAsync(reportId);
        return deleted ? NoContent() : NotFound();
    }
}
