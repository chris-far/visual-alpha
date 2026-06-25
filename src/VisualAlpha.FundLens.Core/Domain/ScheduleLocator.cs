namespace VisualAlpha.FundLens.Core.Domain;

public sealed record ScheduleLocator
{
    public required HeaderPattern StartPattern { get; init; }
    public HeaderPattern? TerminationPattern { get; init; }
    public int PageHint { get; init; }
}
