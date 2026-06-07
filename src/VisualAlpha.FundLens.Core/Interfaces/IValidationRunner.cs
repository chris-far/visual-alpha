using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

/// <summary>Orchestrates all validation rules against an ExtractionResult.</summary>
public interface IValidationRunner
{
    Task<ValidationReport> RunAsync(ExtractionResult result);
}