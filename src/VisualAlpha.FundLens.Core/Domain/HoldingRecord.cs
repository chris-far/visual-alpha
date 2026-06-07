using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

/// <summary>Single extracted holding from a Schedule of Investments table</summary>
public sealed record HoldingRecord
{
    public required string SecurityName { get; init; }
    public string? SecurityType { get; init; }
    public string? Sector { get; init; }
    public string? Country { get; init; } // ISO3
    public decimal? Shares { get; init; }
    public decimal? Principal { get; init; }
    public decimal? MarketValue { get; init; }

    public int SourcePageNumber { get; init; }
    public double ExtractionConfidence { get; init; } = 1.0;
    public CountrySource CountrySource { get; init; }
    public List<string> ExtractionWarnings { get; init; } = [];
}