using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Validation.Rules;

public sealed class SchemaValidationRule : IValidationRule
{
    public string RuleName => "SchemaValidation";
    public ValidationSeverity Severity => ValidationSeverity.Critical;

    public IEnumerable<ValidationFinding> Validate(ExtractionResult result)
    {
        if (!result.Holdings.Any())
        {
            yield return new ValidationFinding
            {
                RuleName = RuleName,
                Message = "No holdings extracted — extraction may have failed",
                Severity = ValidationSeverity.Critical
            };
            yield break;
        }

        for (var i = 0; i < result.Holdings.Count; i++)
        {
            var h = result.Holdings[i];

            if (string.IsNullOrWhiteSpace(h.SecurityName))
                yield return new ValidationFinding
                {
                    RuleName = RuleName,
                    Message = "SecurityName is null or empty",
                    Severity = ValidationSeverity.Error,
                    HoldingIndex = i,
                    FieldName = nameof(h.SecurityName)
                };

            if (h.MarketValue is null && h.Principal is null && h.Shares is null)
                yield return new ValidationFinding
                {
                    RuleName = RuleName,
                    Message = $"Holding '{h.SecurityName}' has no numeric values (marketValue, principal, shares all null)",
                    Severity = ValidationSeverity.Warning,
                    HoldingIndex = i
                };
        }
    }
}
