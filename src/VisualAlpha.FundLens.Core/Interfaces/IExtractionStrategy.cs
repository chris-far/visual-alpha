using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

public interface IExtractionStrategy
{
    bool CanHandle(TableConfig layout);
    Task<List<HoldingRecord>> ExtractHoldingsAsync(
        List<PageStructure> pages,
        ReportLayoutConfig layout,
        ScheduleLocator locator);
}