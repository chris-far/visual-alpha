namespace VisualAlpha.FundLens.Core.Domain;

public sealed record FundScheduleConfig
{
    public required string FundId { get; init; }
    public required string DisplayName { get; init; }
    public string? FundNameRegex { get; init; }
    public required ScheduleLocator ScheduleLocator { get; init; }

    /// <summary>
    /// Fund-level overrides for report-level layout config. Null fields inherit from the report config.
    /// </summary>
    public ReportLayoutConfig? Overrides { get; init; }
}
