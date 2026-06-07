using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Ingestion.ConfigGeneration;

// TODO: column splits can be different across different pages for the same fund/table
// TODO: proper support for multi-line headers and data
public sealed class ColumnRangeResolver(ILogger<ColumnRangeResolver> log) : IColumnRangeResolver
{
    // Blocks whose entire text is whitespace, dots, dashes, underscores, etc. carry no positional signal
    private static readonly Regex NoisyBlock = new(@"^[\s.\-–—_*•·…]+$", RegexOptions.Compiled);
    
    public ReportConfig Resolve(ReportConfig report, PdfStructure structure)
    {
        var reportLayout = report.ReportLayout;

        // Resolve each fund; track which ones used the report-level layout and what they produced
        var results = report.Funds
            .Select(fund =>
            {
                if (fund.Overrides?.TableConfig is not null)
                {
                    var resolved = ResolveTableConfig(fund.Overrides.TableConfig, fund.ScheduleLocator, fund.FundId, structure);
                    return (Fund: fund, Resolved: (TableConfig?)resolved, UsesReportLayout: false);
                }
                if (reportLayout.TableConfig is not null)
                {
                    var resolved = ResolveTableConfig(reportLayout.TableConfig, fund.ScheduleLocator, fund.FundId, structure);
                    return (Fund: fund, Resolved: (TableConfig?)resolved, UsesReportLayout: true);
                }
                return (Fund: fund, Resolved: null, UsesReportLayout: false);
            })
            .ToList();

        // If every fund using the report-level layout resolved identically, apply it there once
        var reportLevelResults = results.Where(r => r is { UsesReportLayout: true, Resolved: not null }).ToList();
        var applyToReportLayout = reportLevelResults.Count > 0 &&
            reportLevelResults.Select(r => r.Resolved!).Distinct(TableConfigEqualityComparer.Instance).Count() == 1;

        if (applyToReportLayout)
            reportLayout = reportLayout with { TableConfig = reportLevelResults[0].Resolved };

        var updatedFunds = results
            .Select(r =>
            {
                // Fund with its own layout: resolved result goes back as a fund override
                if (!r.UsesReportLayout && r.Resolved is not null)
                    return r.Fund with { Overrides = r.Fund.Overrides! with { TableConfig = r.Resolved } };

                // Layouts differ across funds: promote each to a fund override instead
                if (r.UsesReportLayout && !applyToReportLayout && r.Resolved is not null)
                {
                    var overrides = r.Fund.Overrides is not null
                        ? r.Fund.Overrides with { TableConfig = r.Resolved }
                        : new ReportLayoutConfig { TableConfig = r.Resolved };
                    return r.Fund with { Overrides = overrides };
                }

                return r.Fund;
            })
            .ToList();

        return report with { ReportLayout = reportLayout, Funds = updatedFunds };
    }

    private TableConfig ResolveTableConfig(
        TableConfig tableConfig, ScheduleLocator locator, string fundId, PdfStructure structure)
    {
        var fundLines = CollectFundLines(structure.Pages, locator);
        if (fundLines.Count == 0)
        {
            log.LogWarning("Fund {FundId}: no header row found — skipping range resolution", fundId);
            return tableConfig;
        }

        var resolvedColumnGroups = tableConfig.ColumnGroups
            .Select((column, i) => ResolveColumn(column, fundLines.Where(l => l.ColumnIndex == i).ToList(), fundId, i))
            .ToList();

        return tableConfig with { ColumnGroups = resolvedColumnGroups };
    }

    // Collects lines starting from the first line whose blocks contain the column header keys
    // in order, stopping at the termination pattern.
    private static List<TextLine> CollectFundLines(
        List<PageStructure> allPages,
        ScheduleLocator locator)
    {
        var endRegex = locator.TerminationPattern is { Length: > 0 }
            ? new Regex(locator.TerminationPattern, RegexOptions.IgnoreCase)
            : null;

        var lines   = new List<TextLine>();
        var started = false;

        foreach (var page in allPages)
        {
            var currColumnIndex = page.Lines.Any() ? page.Lines[0].ColumnIndex : -1;
            
            foreach (var line in page.Lines)
            {
                if (!started)
                {
                    if (line.IsHeader) started = true;
                    else continue;
                }

                if (started && line.ColumnIndex != currColumnIndex)
                {
                    // Exclude anything above the header line on the next column
                    currColumnIndex = line.ColumnIndex;
                    if (!line.IsHeader)
                    {
                        started = false;
                        continue;
                    }
                }

                if (endRegex?.IsMatch(string.Join(" ", line.Blocks.Select(b => b.Text))) == true)
                {
                    return lines;
                }

                lines.Add(line);
            }
        }

        return lines;
    }

    private ColumnConfig ResolveColumn(ColumnConfig column, List<TextLine> rows, string fundId, int columnIndex)
    {
        if (column.Fields is not { Count: > 0 }) return column;

        var visibleHeaders = column.Fields
            .Where(e => e.IsHeaderTextVisible)
            .Select(x => x.HeaderText)
            .ToList();
        var nonVisibleHeaders = column.Fields
            .Where(e => !e.IsHeaderTextVisible)
            .ToList();

        // Header line: the one whose blocks match the most visibleHeaders header texts
        var headerLine = rows.FirstOrDefault(r => r.IsHeader);

        if (headerLine is null)
        {
            log.LogWarning("Fund {FundId} column {Index}: header row not found", fundId, columnIndex);
            return column;
        }

        // Data rows below the header, with noisy blocks (whitespace/punctuation-only) stripped out
        var headerCount = column.Fields.Count;
        var dataLines = rows
            .Where(l => l.Y < headerLine.Y)
            .Select(l => l with { Blocks = l.Blocks.Where(b => !NoisyBlock.IsMatch(b.Text)).ToList() })
            .Where(l => l.Blocks.Count >= headerCount)
            .ToList();

        // For each header block, find the matching visible header (exact or partial for stacked text),
        // then compute LeftX/RightX from data blocks that align horizontally with the header block.
        var xRanges = new Dictionary<string, (double Left, double Right)>();
        foreach (var headerBlock in headerLine.Blocks)
        {
            var headerText  = headerBlock.Text.Trim();
            var fullHeader = visibleHeaders.FirstOrDefault(h => string.Equals(h, headerText) || h!.Contains(headerText));
            
            if (fullHeader is null) continue;

            // Match data blocks by containment rather than index, since data rows may carry
            // additional blocks for non-visible fields. Accept either direction:
            //   • data block falls within the header block's range (typical case)
            //   • data block is wider and the header block fits inside it (e.g. long cell values)
            var headerBlockIndex = headerLine.Blocks.IndexOf(headerBlock);
            var candidates = new List<TextBlock>();
            
            foreach (var line in dataLines)
            {
                // Data row has same number of elements has header
                if (line.Blocks.Count == visibleHeaders.Count && nonVisibleHeaders.Count == 0)
                {
                    candidates.Add(line.Blocks[headerBlockIndex]);
                    continue;
                }
                
                // Capturing several possibilities here.
                // Right aligned with column header, left-aligned with column header
                // Wider than column header, fits inside column header
                // TODO support jutting to the left or jutting to the right
                const double alignmentTolerance = 2.5;
                var alignedBLocks =
                    line.Blocks
                        .Where(b => Math.Abs(b.Left - headerBlock.Left) <= alignmentTolerance ||
                                    Math.Abs(b.Right - headerBlock.Right) <= alignmentTolerance ||
                                    (headerBlock.Left < b.Left && headerBlock.Right > b.Right) ||
                                    (b.Left <= headerBlock.Left && b.Right > headerBlock.Right))
                        .ToList();
                if (alignedBLocks.Any())
                {
                    candidates.AddRange(alignedBLocks);
                }
            }

            if (candidates.Count == 0)
            {
                // Fallback to using X ranges of the column itself
                candidates.Add(headerBlock);
            }
            
            var left = candidates.Min(b => b.Left);
            var right = candidates.Max(b => b.Right);
            xRanges[fullHeader] = (Math.Floor(left), Math.Ceiling(right));
        }

        // Allocate X space to the single non-visible field using its index position within the
        // ordered field list to determine which neighbouring resolved ranges bound it.
        (double Left, double Right)? nonVisibleRange = null;
        if (nonVisibleHeaders.Count == 1 && xRanges.Count > 0)
        {
            var nonVisible = nonVisibleHeaders[0];
            var ordered = column.Fields.OrderBy(f => f.Index).ToList();
            var pos = ordered.IndexOf(nonVisible);

            var prev = pos > 0 ? ordered[pos - 1] : null;
            var next = pos < ordered.Count - 1 ? ordered[pos + 1] : null;

            var prevRight = prev is not null && xRanges.TryGetValue(prev.HeaderText!, out var pr) ? pr.Right : (double?)null;
            var nextLeft = next is not null && xRanges.TryGetValue(next.HeaderText!, out var nl) ? nl.Left : (double?)null;

            var uncovered = rows
                .SelectMany(l => l.Blocks)
                .Where(b => !NoisyBlock.IsMatch(b.Text) &&
                            (prevRight is null || b.Left > prevRight.Value) &&
                            (nextLeft is null || b.Right < nextLeft.Value))
                .ToList();

            if (uncovered.Count > 0)
                nonVisibleRange = (
                    Math.Floor(uncovered.Min(b => b.Left)),
                    Math.Ceiling(uncovered.Max(b => b.Right)));
        }

        var updatedFields = column.Fields
            .Select(e =>
            {
                return e.IsHeaderTextVisible switch
                {
                    true when xRanges.TryGetValue(e.HeaderText!, out var r) => e with
                    {
                        LeftX = r.Left, 
                        RightX = r.Right
                    },
                    false when nonVisibleRange.HasValue => e with
                    {
                        LeftX = nonVisibleRange.Value.Left, 
                        RightX = nonVisibleRange.Value.Right
                    },
                    _ => e
                };
            })
            .ToList();

        var allRanges = xRanges.Values
            .Concat(nonVisibleRange.HasValue ? [(nonVisibleRange.Value.Left, nonVisibleRange.Value.Right)] : [])
            .ToList();
        var startX = allRanges.Count > 0 ? allRanges.Min(r => r.Left)  : (double?)null;
        var endX   = allRanges.Count > 0 ? allRanges.Max(r => r.Right) : (double?)null;

        log.LogInformation(
            "Fund {FundId} column {Index}: resolved {Count} visible + {NonVisible} non-visible ranges from {Rows} data rows: {Fields}",
            fundId, columnIndex, xRanges.Count, nonVisibleRange.HasValue ? 1 : 0, dataLines.Count,
            string.Join(", ", xRanges.Keys));

        return new ColumnConfig { Fields = updatedFields, StartX = startX, EndX = endX };
    }
}
