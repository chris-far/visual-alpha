using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Interfaces;

/// <summary>A single composable validation rule</summary>
public interface IValidationRule
{
    string RuleName { get; }
    ValidationSeverity Severity { get; }
    IEnumerable<ValidationFinding> Validate(ExtractionResult result);
}