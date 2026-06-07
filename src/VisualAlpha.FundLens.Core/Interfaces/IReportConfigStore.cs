using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

public interface IReportConfigStore
{
    Task<ReportConfig?> GetAsync(string reportId);
    Task SaveAsync(ReportConfig config);
    Task<IReadOnlyList<ReportConfig>> GetAllAsync();
}
