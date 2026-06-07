namespace VisualAlpha.FundLens.Core.Domain;

public sealed record ScheduleLocator
{
    public required string StartPattern { get; init; }
    public string? TerminationPattern { get; init; }
    public int PageHint { get; init; }
}
