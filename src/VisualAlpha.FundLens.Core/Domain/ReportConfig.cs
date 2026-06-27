using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

public sealed record ReportConfig
{
    public required string ReportId { get; init; }
    public required ReportType ReportType { get; init; }
    public string? Publisher { get; init; }
    public string? DisplayName { get; init; }
    public int Version { get; init; } = 1;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public required ReportLayoutConfig ReportLayout { get; init; }
    public List<FundScheduleConfig> Funds { get; init; } = [];

    public List<string> Issues { get; init; } = [];
    public double ConfidenceScore { get; init; }
}
