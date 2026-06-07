using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Validation.Rules;

public sealed class ConfidenceThresholdRule(double lowThreshold = 0.7, double criticalFraction = 0.05)
    : IValidationRule
{
    public string RuleName => "ConfidenceThreshold";
    public ValidationSeverity Severity => ValidationSeverity.Warning;

    public IEnumerable<ValidationFinding> Validate(ExtractionResult result)
    {
        var lowConfidence = result.Holdings
            .Select((h, i) => (h, i))
            .Where(t => t.h.ExtractionConfidence < lowThreshold)
            .ToList();

        foreach (var (h, i) in lowConfidence)
            yield return new ValidationFinding
            {
                RuleName = RuleName,
                Message = $"Low confidence ({h.ExtractionConfidence:P0}): {h.SecurityName}. Warnings: {string.Join("; ", h.ExtractionWarnings)}",
                Severity = ValidationSeverity.Warning,
                HoldingIndex = i
            };

        var critFrac = (double)lowConfidence.Count / Math.Max(1, result.Holdings.Count);
        if (critFrac >= criticalFraction)
            yield return new ValidationFinding
            {
                RuleName = RuleName,
                Message = $"{critFrac:P0} of holdings have low confidence (>{criticalFraction:P0} threshold) — human review recommended",
                Severity = ValidationSeverity.Error
            };
    }
}
