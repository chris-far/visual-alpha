using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Ingestion;

public sealed class ReportOnboardingService(
    IReportConfigGenerator configGenerator,
    IPdfPreProcessor preProcessor,
    IColumnRangeResolver columnRangeResolver,
    IHoldingExtractor extractor) : IReportOnboardingService
{
    public async Task<OnboardingResult> OnboardAsync(Stream pdfStream)
    {
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms);
        var pdfBytes = ms.ToArray();

        using var configStream = new MemoryStream(pdfBytes);
        var draft = await configGenerator.GenerateAsync(configStream);

        using var preProcessStream = new MemoryStream(pdfBytes);
        var structure = await preProcessor.AnalyseAsync(preProcessStream, draft);
        var report = columnRangeResolver.Resolve(draft, structure);

        using var extractStream = new MemoryStream(pdfBytes);
        var extractions = await extractor.ExtractAsync(extractStream, report);

        return new OnboardingResult(report, extractions);
    }
}
