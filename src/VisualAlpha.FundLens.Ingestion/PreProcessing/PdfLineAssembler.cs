using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using PdfTextLine = UglyToad.PdfPig.DocumentLayoutAnalysis.TextLine;
using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Ingestion.PreProcessing;

internal static class PdfLineAssembler
{
    internal static List<TextLine> BuildSortedPageLinesByColumn(Page page, ReportLayoutConfig layout)
    {
        var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
        var rawBlocks = RecursiveXYCut.Instance.GetBlocks(words);

        // Flatten to individual text lines, ordered top-to-bottom then left-to-right
        var allLines = rawBlocks
            .SelectMany(b => b.TextLines)
            .OrderByDescending(l => l.BoundingBox.Centroid.Y)
            .ThenBy(l => l.BoundingBox.Left)
            .ToList();

        // Group lines into rows: two lines belong to the same row if their tops or bottoms are within 0.25pt
        var rows = GroupIntoRows(allLines);

        // For 2-column layouts, determine where the right pane starts
        var splitX = FindColumnSplitX(page, layout);

        var result = new List<TextLine>();
        foreach (var row in rows)
        {
            var cols = layout.TableConfig?.ColumnGroups;
            var ordered = row.OrderBy(l => l.BoundingBox.Left).ToList();
            if (splitX is not null)
            {
                var left  = ordered.Where(l => l.BoundingBox.Left <  splitX).ToList();
                var right = ordered.Where(l => l.BoundingBox.Left >= splitX).ToList();
                
                if (left.Count  > 0) AddTextLine(left,  columnIndex: 0);
                if (right.Count > 0) AddTextLine(right, columnIndex: 1);
            }
            else
            {
                AddTextLine(ordered, columnIndex: 0);
            }

            continue;

            void AddTextLine(List<PdfTextLine> line, int columnIndex)
            {
                var col = cols?[columnIndex];
                var isHeader = IsHeader(rows, line, col);
                result.Add(ToTextLine(line, columnIndex, isHeader));
            }
        }

        return result
            .OrderBy(x => x.ColumnIndex)
            .ThenByDescending(x => x.Y)
            .ToList();
    }

    // Groups lines into rows: lines are merged when their top edges or bottom edges are within `tolerance` pt.
    // Input must be sorted top-to-bottom (descending Y). Each returned list is sorted left-to-right.
    private static List<List<PdfTextLine>> GroupIntoRows(List<PdfTextLine> lines)
    {
        const double tolerance = 0.25;
        var clusters = new List<List<PdfTextLine>>();
        if (lines.Count == 0) return clusters;

        var current = new List<PdfTextLine> { lines[0] };
        var curTop  = lines[0].BoundingBox.Top;
        var curBot  = lines[0].BoundingBox.Bottom;

        for (var i = 1; i < lines.Count; i++)
        {
            var top = lines[i].BoundingBox.Top;
            var bot = lines[i].BoundingBox.Bottom;

            if (Math.Abs(top - curTop) <= tolerance || Math.Abs(bot - curBot) <= tolerance)
            {
                current.Add(lines[i]);
                curTop = Math.Max(curTop, top);
                curBot = Math.Min(curBot, bot);
            }
            else
            {
                clusters.Add(current.OrderBy(l => l.BoundingBox.Left).ToList());
                current = [lines[i]];
                curTop  = top;
                curBot  = bot;
            }
        }

        clusters.Add(current.OrderBy(l => l.BoundingBox.Left).ToList());
        return clusters;
    }

    private static double? FindColumnSplitX(Page page, ReportLayoutConfig layout)
    {
        // TODO: better split detection if we come across a multi-column report with asymmetrical table distribution
        // For now, splitting down the middle of the page
        var cols = layout.TableConfig?.ColumnGroups;
        return cols?.Count == 2 ? page.Width / 2 : null;
    }

    private static TextLine ToTextLine(List<PdfTextLine> pdfLines, int columnIndex, bool isHeader = false)
    {
        var top = pdfLines.Max(l => l.BoundingBox.Top);
        var blocks = pdfLines.Select(l => new TextBlock
        {
            Text = l.Text.Trim(),
            X = l.BoundingBox.Left,
            Y = l.BoundingBox.Top,
            Left = l.BoundingBox.Left,
            Right = l.BoundingBox.Right,
            Width = l.BoundingBox.Width,
            Height = l.BoundingBox.Height,
            IsBold = l.Words.Any(PdfFontDetector.IsBold),
            ColumnIndex = columnIndex
        }).ToList();

        return new TextLine
        {
            Blocks = blocks,
            ColumnIndex = columnIndex,
            IsHeader = isHeader,
            X = pdfLines[0].BoundingBox.Left,
            Y = top
        };
    }

    // Matches a row against the visible header fields of a column.
    // Supports columns where one field has no visible header text (IsHeaderTextVisible = false) —
    // that field is simply excluded from the line count and match, so a column with N fields
    // where 1 is non-visible still matches a row of N-1 lines.
    private static bool IsHeader(List<List<PdfTextLine>> allLines, List<PdfTextLine> lines, ColumnConfig? col)
    {
        if (col is null || lines.Count == 0 || col.Fields == null || col.Fields.Count == 0)
        {
            return false;
        }

        // Only match against fields that physically appear as header text on the page
        var visibleHeaders = col.Fields
            .Where(f => f.IsHeaderTextVisible)
            .Select(f => f.HeaderText!)
            .ToList();

        if (lines.Count != visibleHeaders.Count)
        {
            return false;
        }

        var matched = 0;
        foreach (var line in lines)
        {
            if (visibleHeaders.Any(h => string.Equals(h, line.Text)) ||
                visibleHeaders.Any(h => h.StartsWith(line.Text) && h.Length - line.Text.Length <= 1))
            {
                matched++;
            }
            else if (visibleHeaders.Any(h => h.Contains(line.Text)))
            {
                // Try to detect multi-line column header
                // Look a maximum two lines above the current line
                var currHeader = line.Text;
                var targetLine = allLines.SingleOrDefault(x => x.Intersect(lines).Any());
                if (targetLine is null) continue;
                var lineIndex = allLines.IndexOf(targetLine);
                var multilineMatch = false;
                var innerIndex = 1;
                
                while (innerIndex <= 2 && !multilineMatch)
                {
                    var prevLine = allLines.ElementAtOrDefault(lineIndex - innerIndex);
                    innerIndex++;
                    if (prevLine is null) continue;
                    foreach (var prev in prevLine)
                    {
                        var potentialHeader = $"{prev.Text} {currHeader}";
                        if (visibleHeaders.Any(h => string.Equals(h, potentialHeader)))
                        {
                            matched++;
                            multilineMatch = true;
                            break;
                        }
                        if (visibleHeaders.Any(h => h.Contains(potentialHeader)))
                        {
                            currHeader = potentialHeader;
                            break;
                        }
                    }
                }
            }
        }

        return matched == visibleHeaders.Count;
    }
}
