namespace VisualAlpha.FundLens.Validation.Monitoring;

public sealed record LayoutDriftAlert
{
    public required string ReportId { get; init; }
    public required string Publisher { get; init; }
    public DateTime DetectedAt { get; init; }
    public List<string> DriftReasons { get; init; } = [];
}