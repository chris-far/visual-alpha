namespace VisualAlpha.FundLens.Core.Domain;

public sealed record HeaderPattern
{
    public string? Regex { get; init; }
    public string? Example { get; init; }
    public bool? IsBold { get; init; }
    public bool? SpansFullWidth { get; init; }
}
