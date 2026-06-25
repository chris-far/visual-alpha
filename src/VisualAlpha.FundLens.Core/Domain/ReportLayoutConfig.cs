using System.Reflection;
using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

/// <summary>
/// Layout rules that apply across all funds in a report.
/// Also used as the overrides type on FundScheduleConfig — null fields mean "inherit from report-level".
/// </summary>
public sealed record ReportLayoutConfig
{
    private static readonly PropertyInfo[] Props =
        typeof(ReportLayoutConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();
    
    public TableConfig? TableConfig { get; init; }
    public HeaderPattern? ReportDatePattern { get; init; }
    public HeaderPattern? SecurityTypePattern { get; init; }
    public HeaderPattern? SectorPattern { get; init; }
    public HeaderPattern? CountryPattern { get; init; }
    public HeaderPattern? SecurityNameCleaningPattern { get; init; }
    public HeaderPattern? FootnotePattern { get; init; }
    public HeaderPattern? SubtotalRowPattern { get; init; }
    public string? CurrencySymbol { get; init; }
    public NegativeNotation? NegativeNotation { get; init; }
    public double? ValidationTolerance { get; init; }

    // Merges overrides into this layout: each non-null override property wins, null means inherit.
    // Reflection-based so new properties are picked up automatically without touching this method.
    public ReportLayoutConfig MergeWith(ReportLayoutConfig? overrides)
    {
        if (overrides is null) return this;

        var result = new ReportLayoutConfig();
        foreach (var prop in Props)
        {
            prop.SetValue(result, prop.GetValue(overrides) ?? prop.GetValue(this));
        }
        return result;
    }
}
