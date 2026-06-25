using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;
using VisualAlpha.FundLens.Extraction.Core;

namespace VisualAlpha.FundLens.Extraction.Strategies;

public sealed class ColumnBasedExtractionStrategy(ILogger<ColumnBasedExtractionStrategy> log) : IExtractionStrategy
{
    public bool CanHandle(TableConfig layout) => layout.ColumnGroups.Count >= 1;

    public async Task<List<HoldingRecord>> ExtractHoldingsAsync(
        List<PageStructure> pages,
        ReportLayoutConfig layout,
        ScheduleLocator locator)
    {
        return await Task.Run(() => ExtractAll(pages, layout, locator));
    }

    private List<HoldingRecord> ExtractAll(List<PageStructure> pages, ReportLayoutConfig layout, ScheduleLocator locator)
    {
        var columnGroups = layout.TableConfig?.ColumnGroups ?? [];
        var parsers = columnGroups.Select(_ => new RowParser(layout)).ToList();
        var holdings = new List<HoldingRecord>();

        var startRegex = new Regex(locator.StartPattern.Regex!, RegexOptions.IgnoreCase);
        var terminationRegex = locator.TerminationPattern?.Regex is { Length: > 0 }
            ? new Regex(locator.TerminationPattern.Regex!, RegexOptions.IgnoreCase)
            : null;

        var state = new ScheduleState { Started = false, Terminated = false };

        foreach (var page in pages)
        {
            if (state.Terminated) break;

            for (var i = 0; i < columnGroups.Count; i++)
            {
                var columnGroup = columnGroups[i];
                var fields = columnGroup.Fields ?? [];

                var columnGroupRows = page.Lines
                    .Where(b => b.ColumnIndex == i)
                    .OrderByDescending(x => x.Y)
                    .ToList();

                var parsedHoldings = ParseColumnGroup(columnGroupRows, fields, parsers[i], page.PageNumber, startRegex, terminationRegex, state);
                holdings.AddRange(parsedHoldings);
            }

            log.LogDebug("Page {Page}: {Count} holdings (running total)", page.PageNumber, holdings.Count);
        }

        return holdings;
    }

    private static List<HoldingRecord> ParseColumnGroup(
        List<TextLine> rows,
        IReadOnlyList<FieldEntry> fields,
        RowParser parser,
        int pageNumber,
        Regex startRegex,
        Regex? terminationRegex,
        ScheduleState state)
    {
        if (rows.Count == 0 || state.Terminated) return [];

        var requiredFields = fields
            .Select(e => e.Field)
            .ToHashSet();

        var holdings = new List<HoldingRecord>();
        foreach (var row in rows)
        {
            var rowText = row.Text;

            if (!state.Started)
            {
                if (startRegex.IsMatch(rowText) || row.IsHeader)
                {
                    state.Started = true;
                    state.ColumnIndex = row.ColumnIndex;
                    if (row.IsHeader) continue;
                }
                else continue;
            }
            
            // A repeated column header row or change in column index signals the start of the next column — reset state
            if ((holdings.Count > 0 && row.IsHeader) || row.ColumnIndex != state.ColumnIndex)
            {
                state.Started = false;
                state.ColumnIndex = row.ColumnIndex;
                continue;
            }

            if (terminationRegex?.IsMatch(rowText) == true)
            {
                state.Terminated = true;
                break;
            }

            var mapped  = ColumnMapper.MapRow(row.Blocks, fields);
            var holding = parser.TryParseRow(mapped, row.Blocks, pageNumber, requiredFields);
            if (holding is not null) holdings.Add(holding);
        }
        return holdings;
    }

    private sealed class ScheduleState
    {
        public bool Started    { get; set; }
        public bool Terminated { get; set; }
        public int ColumnIndex { get; set; }
    }
}
