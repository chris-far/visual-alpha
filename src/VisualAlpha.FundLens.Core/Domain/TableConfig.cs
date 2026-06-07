namespace VisualAlpha.FundLens.Core.Domain;

/// <summary>
/// Describes how the holdings table is laid out across the page.
/// Single-column: one column group. Double-column: two column groups.
/// Column boundaries are auto-detected from column x-position clustering; no explicit split coordinates needed.
/// </summary>
public sealed record TableConfig
{
    public List<ColumnConfig> ColumnGroups { get; init; } = [];
}
