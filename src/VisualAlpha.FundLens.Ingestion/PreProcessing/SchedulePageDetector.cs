using System.Text.RegularExpressions;

namespace VisualAlpha.FundLens.Ingestion.PreProcessing;

internal static class SchedulePageDetector
{
    private static readonly Regex ScheduleHeader = new(
        @"\b(?:Consolidated\s+|Summary\s+|Condensed\s+|Supplemental\s+)?Schedule\s+of\s+(?:Investment\s+Portfolio|Portfolio\s+Investments|Investments)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static bool IsSchedulePage(string pageText) =>
        ScheduleHeader.IsMatch(pageText);
}
