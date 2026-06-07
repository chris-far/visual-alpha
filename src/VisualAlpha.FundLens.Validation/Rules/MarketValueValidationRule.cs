using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Validation.Rules;

/// <summary>
/// Validates that the sum of all extracted market values is internally consistent.
/// In the absence of a reported total from the PDF, checks for reasonable range.
/// </summary>
public sealed class MarketValueSumRule : IValidationRule
{
    public string RuleName => "MarketValueSum";
    public ValidationSeverity Severity => ValidationSeverity.Warning;

    public IEnumerable<ValidationFinding> Validate(ExtractionResult result)
    {
        var valueHoldings = result.Holdings
            .Where(h => h.MarketValue.HasValue && h.MarketValue > 0)
            .ToList();

        if (valueHoldings.Count < 5) yield break;   // Not enough data to check

        var totalMv = valueHoldings.Sum(h => h.MarketValue!.Value);
        var nullCount = result.Holdings.Count - valueHoldings.Count;

        // More than 10% of rows have no market value → suspicious
        var nullFraction = (double)nullCount / result.Holdings.Count;
        if (nullFraction > 0.10)
            yield return new ValidationFinding
            {
                RuleName = RuleName,
                Message = $"{nullFraction:P0} of holdings have no market value — check column mapping",
                Severity = ValidationSeverity.Warning
            };

        // Sanity: market values should be positive non-trivial
        if (totalMv < 1_000)
            yield return new ValidationFinding
            {
                RuleName = RuleName,
                Message = $"Total extracted market value ({totalMv:N0}) is implausibly small — possible parsing error",
                Severity = ValidationSeverity.Error
            };
    }
}