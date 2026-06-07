namespace VisualAlpha.FundLens.Core.Domain;

public sealed record ColumnConfig
{
    public List<FieldEntry>? Fields { get; init; }  // ordered field entries with resolved X boundaries; LLM-provided, populated by ColumnRangeResolver
    public double? StartX { get; init; }
    public double? EndX { get; init; }
}
