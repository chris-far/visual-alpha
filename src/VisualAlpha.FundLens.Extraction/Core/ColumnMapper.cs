using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Extraction.Core;

public static class ColumnMapper
{
    public static Dictionary<FieldType, string> MapRow(IEnumerable<TextBlock> rowBlocks, IReadOnlyList<FieldEntry> fields)
    {
        var result = new Dictionary<FieldType, string>();

        foreach (var block in rowBlocks)
        {
            foreach (var entry in fields)
            {
                if (block.X < entry.LeftX || block.X > entry.RightX)
                {
                    continue; // out of bounds of column
                }

                if (result.TryGetValue(entry.Field, out var existing))
                {
                    result[entry.Field] = $"{existing} {block.Text}";
                }
                else
                {
                    result[entry.Field] = block.Text;
                }

                break;
            }
        }

        return result;
    }
}
