using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

public interface IHoldingExtractor
{
    Task<IReadOnlyList<ExtractionResult>> ExtractAsync(Stream pdfStream, ReportConfig report);
}