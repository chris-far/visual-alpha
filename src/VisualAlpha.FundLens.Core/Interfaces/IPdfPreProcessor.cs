using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

/// <summary>Extracts structural information from a PDF</summary>
public interface IPdfPreProcessor
{
    Task<PdfStructure> AnalyseAsync(Stream pdfStream, ReportConfig report);
}