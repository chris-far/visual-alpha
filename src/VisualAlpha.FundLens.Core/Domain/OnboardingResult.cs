namespace VisualAlpha.FundLens.Core.Domain;

public sealed record OnboardingResult(ReportConfig Report, IReadOnlyList<ExtractionResult> Extractions);
