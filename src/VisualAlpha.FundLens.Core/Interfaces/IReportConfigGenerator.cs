using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

public interface IReportConfigGenerator
{
    Task<ReportConfig> GenerateAsync(Stream pdfStream);
}
