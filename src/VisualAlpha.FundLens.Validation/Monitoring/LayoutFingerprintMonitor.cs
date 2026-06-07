using Microsoft.Extensions.Logging;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Validation.Monitoring;

/// <summary>
/// Detects layout drift in a fund's PDF by comparing column x-positions and
/// header patterns against what the current config expects.
/// Run monthly before the batch extraction cycle for proactive alerts.
/// </summary>
public sealed class LayoutFingerprintMonitor(
    IPdfPreProcessor preProcessor,
    IReportConfigStore configStore,
    ILogger<LayoutFingerprintMonitor> log)
{
    public async Task<IReadOnlyList<LayoutDriftAlert>> CheckAllAsync(IReadOnlyDictionary<string, Stream> reportIdToPdfStream)
    {
        var alerts = new List<LayoutDriftAlert>();

        foreach (var (reportId, pdfStream) in reportIdToPdfStream)
        {
            var report = await configStore.GetAsync(reportId);
            if (report is null) continue;

            var structure = await preProcessor.AnalyseAsync(pdfStream, report);
            var alert = Analyse(report, structure);
            if (alert is not null) alerts.Add(alert);
        }

        return alerts;
    }

    private LayoutDriftAlert? Analyse(ReportConfig report, PdfStructure structure)
    {
        var drifts = new List<string>();

        var expectedColumnGroupCount = report.ReportLayout.TableConfig?.ColumnGroups.Count ?? 0;
        var detectedColumnGroupCount = structure.LikelyLayout == TableLayout.DoubleColumn ? 2 : 1;
        if (expectedColumnGroupCount > 0 && expectedColumnGroupCount != detectedColumnGroupCount)
            drifts.Add($"Column group count changed: config expects {expectedColumnGroupCount}, detected {detectedColumnGroupCount}");

        if (string.IsNullOrEmpty(structure.ScheduleStartExcerpt))
            drifts.Add("Schedule of Investments header not found — report format may have changed significantly");

        // var configColumnCount = report.ReportLayout.TableConfig?.ColumnGroups.Sum(p => p.Fields?.Count ?? 0) ?? 0;
        // var detectedColumnCount = structure.Pages.FirstOrDefault(p => p.LikelyContainsSchedule)?.ColumnXPositions.Count ?? 0;
        //
        // if (detectedColumnCount > 0 && Math.Abs(detectedColumnCount - configColumnCount) > 1)
        //     drifts.Add($"Column count changed: config expects {configColumnCount}, detected {detectedColumnCount}");

        if (drifts.Count == 0) return null;

        log.LogWarning("Layout drift detected for {ReportId}: {Drifts}", report.ReportId, string.Join("; ", drifts));

        return new LayoutDriftAlert
        {
            ReportId = report.ReportId,
            Publisher = report.Publisher ?? report.ReportId,
            DetectedAt = DateTime.UtcNow,
            DriftReasons = drifts
        };
    }
}