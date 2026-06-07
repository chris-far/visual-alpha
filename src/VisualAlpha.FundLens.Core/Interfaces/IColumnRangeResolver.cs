using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

public interface IColumnRangeResolver
{
    ReportConfig Resolve(ReportConfig report, PdfStructure structure);
}
