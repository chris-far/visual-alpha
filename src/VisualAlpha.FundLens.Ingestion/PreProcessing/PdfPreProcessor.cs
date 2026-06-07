using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Ingestion.PreProcessing;

/// <summary>
/// Uses PdfPig to extract text blocks with positions and structural signals from a fund PDF
/// </summary>
public sealed class PdfPreProcessor(ILogger<PdfPreProcessor> log) : IPdfPreProcessor
{
    public async Task<PdfStructure> AnalyseAsync(Stream pdfStream, ReportConfig report)
    {
        // PdfPig is synchronous; offload to thread pool so callers stay async
        return await Task.Run(() => AnalysePdf(pdfStream, report));
    }

    private PdfStructure AnalysePdf(Stream pdfStream, ReportConfig report)
    {
        var sw = Stopwatch.StartNew();
        using var pdf = PdfDocument.Open(pdfStream);
        var pages = new List<PageStructure>();
        var foundSchedule = false;
        string? scheduleExcerpt = null;

        foreach (var page in pdf.GetPages())
        {
            var lines = PdfLineAssembler.BuildSortedPageLinesByColumn(page, report.ReportLayout);
            
            var hasSchedule = SchedulePageDetector.IsSchedulePage(page.Text);
            if (hasSchedule && !foundSchedule)
            {
                foundSchedule = true;
                scheduleExcerpt = page.Text[..80];
            }

            pages.Add(new PageStructure
            {
                PageNumber = page.Number,
                Width = page.Width,
                LikelyContainsSchedule = hasSchedule,
                Lines = lines
            });
        }

        sw.Stop();
        log.LogInformation("PDF analysed: {Pages} pages, schedule found={Found}, elapsed={Elapsed}ms",
            pdf.NumberOfPages, foundSchedule, sw.ElapsedMilliseconds);

        return new PdfStructure
        {
            PageCount = pdf.NumberOfPages,
            Pages = pages,
            ScheduleStartExcerpt = scheduleExcerpt
        };
    }
}
