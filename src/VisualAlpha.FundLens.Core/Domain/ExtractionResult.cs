using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

/// <summary>Full result of a single extraction run for one fund</summary>
public sealed record ExtractionResult
{
    public required string FundId { get; init; }
    public required string FundName { get; init; }
    public DateOnly ReportDate { get; init; }
    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;
    public required string ConfigVersion { get; init; }
    public ExtractionStatus Status { get; init; }
    public List<HoldingRecord> Holdings { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
