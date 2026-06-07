using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Ingestion.ConfigGeneration;

internal sealed class TableConfigEqualityComparer : IEqualityComparer<TableConfig>
{
    public static readonly TableConfigEqualityComparer Instance = new();

    private TableConfigEqualityComparer() { }

    public bool Equals(TableConfig? a, TableConfig? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.ColumnGroups.Count != b.ColumnGroups.Count) return false;
        for (var i = 0; i < a.ColumnGroups.Count; i++)
        {
            var ca = a.ColumnGroups[i];
            var cb = b.ColumnGroups[i];
            if (!NullableDoubleEq(ca.StartX, cb.StartX) || !NullableDoubleEq(ca.EndX, cb.EndX)) return false;
            var fa = ca.Fields ?? [];
            var fb = cb.Fields ?? [];
            if (fa.Count != fb.Count) return false;
            for (var j = 0; j < fa.Count; j++)
            {
                if (!DoubleEq(fa[j].LeftX, fb[j].LeftX) || !DoubleEq(fa[j].RightX, fb[j].RightX)) return false;
            }
        }
        return true;
    }

    public int GetHashCode(TableConfig obj) => obj.ColumnGroups.Count;

    // Values are Math.Floor/Ceiling'd, so a tolerance of 0.5 safely distinguishes different positions
    private static bool DoubleEq(double a, double b) => Math.Abs(a - b) < 0.5;
    private static bool NullableDoubleEq(double? a, double? b) =>
        a.HasValue == b.HasValue && (!a.HasValue || DoubleEq(a.Value, b!.Value));
}
