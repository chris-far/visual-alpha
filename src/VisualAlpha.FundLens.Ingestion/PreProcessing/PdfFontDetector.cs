using UglyToad.PdfPig.Content;

namespace VisualAlpha.FundLens.Ingestion.PreProcessing;

internal static class PdfFontDetector
{
    // PdfPig exposes font name; most bold fonts contain "Bold" or "Heavy"
    internal static bool IsBold(Word word)
    {
        var fontName = word.Letters.FirstOrDefault()?.FontName ?? string.Empty;
        return fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase)
            || fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase);
    }
}
