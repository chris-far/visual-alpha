namespace VisualAlpha.FundLens.Core.Domain;

public sealed record TextBlock
{
    public required string Text { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Left { get; init; }
    public double Right { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsBold { get; init; }
    public int ColumnIndex { get; init; }
}
