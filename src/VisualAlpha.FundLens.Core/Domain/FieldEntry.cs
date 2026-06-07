using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

public sealed record FieldEntry
{
    public required FieldType Field { get; init; }
    public string? HeaderText { get; init; }
    public bool IsHeaderTextVisible => HeaderText is not null;
    public int Index { get; init; }
    public double LeftX { get; init; }   // left X boundary (set by ColumnRangeResolver)
    public double RightX { get; init; }  // right X boundary (set by ColumnRangeResolver)
}
