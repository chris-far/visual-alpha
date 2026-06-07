namespace VisualAlpha.FundLens.Core.Domain;

public sealed record TextLine
{
    public required List<TextBlock> Blocks { get; init; }
    public bool IsHeader { get; init; }
    public int ColumnIndex { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public string Text => string.Join(" ", Blocks.Select(x => x.Text));
}

public static class TextLineExtensions
{
    public static IEnumerable<TextBlock> Blocks(this IEnumerable<TextLine> lines)
        => lines.SelectMany(l => l.Blocks);
}
