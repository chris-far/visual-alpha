using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Extraction.Core;

public sealed class HoldingExtractor(
    IPdfPreProcessor preProcessor,
    IEnumerable<IExtractionStrategy> strategies,
    ICountryEnricher countryEnricher,
    ILogger<HoldingExtractor> log) : IHoldingExtractor
{
    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(Stream pdfStream, ReportConfig report)
    {
        log.LogInformation("Starting extraction for report {ReportId} ({FundCount} funds)", report.ReportId, report.Funds.Count);

        var structure = await preProcessor.AnalyseAsync(pdfStream, report);
        var reportDate = ExtractReportDate(structure, report.ReportLayout);
        var results = new List<ExtractionResult>();

        foreach (var fund in report.Funds)
        {
            var layout = report.ReportLayout.MergeWith(fund.Overrides);
            var columnLayout = layout.TableConfig;

            if (columnLayout is null)
            {
                log.LogWarning("No TableConfig configured — skipping fund {FundId}", fund.FundId);
                results.Add(Failed(fund, report.Version, "No TableConfig configured"));
                continue;
            }

            var strategy = strategies.FirstOrDefault(s => s.CanHandle(columnLayout));
            if (strategy is null)
            {
                log.LogWarning("No strategy for {ColumnCount}-column layout — skipping fund {FundId}", columnLayout.ColumnGroups.Count, fund.FundId);
                results.Add(Failed(fund, report.Version, $"No extraction strategy for {columnLayout.ColumnGroups.Count}-column layout"));
                continue;
            }

            var schedulePages = FindSchedulePages(structure, fund.ScheduleLocator);
            if (schedulePages.Count == 0)
            {
                log.LogWarning("No schedule pages found for fund {FundId}", fund.FundId);
                results.Add(Failed(fund, report.Version, "No pages matched scheduleLocator.startPattern"));
                continue;
            }

            var holdings = await strategy.ExtractHoldingsAsync(schedulePages, layout, fund.ScheduleLocator);
            EnrichCountries(holdings, layout);

            log.LogInformation("Fund {FundId}: {Count} holdings extracted", fund.FundId, holdings.Count);

            results.Add(new ExtractionResult
            {
                FundId = fund.FundId,
                FundName = fund.DisplayName,
                ReportDate = reportDate,
                ConfigVersion = report.Version.ToString(),
                Status = holdings.Count > 0 ? ExtractionStatus.Success : ExtractionStatus.Failed,
                Holdings = holdings
            });
        }

        return results;
    }

    private static List<PageStructure> FindSchedulePages(PdfStructure structure, ScheduleLocator locator)
    {
        var startRegex = new Regex(locator.StartPattern, RegexOptions.IgnoreCase);
        var endRegex = locator.TerminationPattern is not null
            ? new Regex(locator.TerminationPattern, RegexOptions.IgnoreCase)
            : null;

        var result = new List<PageStructure>();
        var inSchedule = false;

        foreach (var page in structure.Pages.OrderBy(p => p.PageNumber))
        {
            if (!inSchedule)
            {
                if (startRegex.IsMatch(page.Text)) inSchedule = true;
            }

            if (inSchedule)
            {
                result.Add(page);
                // Stop after the page that contains the termination marker (it belongs to the next fund
                // but may still carry the last few holdings of this one)
                if (endRegex is not null && endRegex.IsMatch(page.Text))
                    break;
            }
        }

        return result;
    }

    private void EnrichCountries(List<HoldingRecord> holdings, ReportLayoutConfig layout)
    {
        var hasCountryColumn = layout.TableConfig?.ColumnGroups
            .Any(g => g.Fields?.Any(e => e.Field == FieldType.Country) == true) == true;
        if (hasCountryColumn) return;

        for (var i = 0; i < holdings.Count; i++)
        {
            var h = holdings[i];
            var enriched = countryEnricher.Enrich(h.Country, h.SecurityName);
            var didEnrich = enriched is not null && enriched != h.Country;
            holdings[i] = h with
            {
                Country = didEnrich ? enriched : h.Country,
                CountrySource = didEnrich ? CountrySource.Enrichment
                    : h.Country is not null ? CountrySource.FromPdf
                    : h.CountrySource
            };
        }
    }

    private DateOnly ExtractReportDate(PdfStructure structure, ReportLayoutConfig layout)
    {
        if (layout.ReportDateRegex is null)
        {
            log.LogWarning("Failed to extract report date, defaulting to today");
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var fullText = string.Join(" ", structure.Pages.SelectMany(p => p.Text));
        var match = Regex.Match(fullText, layout.ReportDateRegex, RegexOptions.IgnoreCase);
        if (!match.Success) return DateOnly.FromDateTime(DateTime.UtcNow);

        return DateOnly.TryParse(match.Value, out var date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static ExtractionResult Failed(FundScheduleConfig fund, int version, string error) => new()
    {
        FundId = fund.FundId,
        FundName = fund.DisplayName,
        ConfigVersion = version.ToString(),
        Status = ExtractionStatus.Failed,
        ErrorMessage = error
    };
}