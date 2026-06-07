namespace VisualAlpha.FundLens.Core.Domain;

public sealed record PageStructure
{
    public int PageNumber { get; init; }
    public double Width { get; init; }
    public bool LikelyContainsSchedule { get; init; }
    public List<TextLine> Lines { get; init; } = [];

    public string Text => string.Join(" ", Lines.Blocks().Select(b => b.Text));
}
