using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Core.Domain;

/// <summary>PDF structural analysis — output of the pre-processor, input to <see cref="IReportConfigGenerator"/></summary>
public sealed record PdfStructure
{
    public int PageCount { get; init; }
    public List<PageStructure> Pages { get; init; } = [];
    public string? ScheduleStartExcerpt { get; init; }
    public TableLayout LikelyLayout { get; init; }
}