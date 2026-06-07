using Microsoft.Extensions.Logging;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Validation;

/// <summary>
/// Orchestrates all registered IValidationRule implementations against an ExtractionResult.
/// Rules are injected via DI — adding a new rule requires only registering it in Program.cs.
/// </summary>
public sealed class ValidationRunner(IEnumerable<IValidationRule> rules, ILogger<ValidationRunner> log) : IValidationRunner
{
    public Task<ValidationReport> RunAsync(ExtractionResult result)
    {
        log.LogInformation("Running validation for {FundId} ({Holdings} holdings)", result.FundId, result.Holdings.Count);

        var findings = rules
            .SelectMany(r =>
            {
                try { return r.Validate(result); }
                catch (Exception ex)
                {
                    log.LogError(ex, "Validation rule {Rule} threw exception", r.RuleName);
                    return [new ValidationFinding
                    {
                        RuleName = r.RuleName,
                        Message = $"Rule threw exception: {ex.Message}",
                        Severity = ValidationSeverity.Error
                    }];
                }
            })
            .ToList();

        var passed = !findings.Any(f => f.Severity >= ValidationSeverity.Error);

        log.LogInformation("Validation complete: passed={Passed}, errors={Errors}, warnings={Warnings}",
            passed,
            findings.Count(f => f.Severity >= ValidationSeverity.Error),
            findings.Count(f => f.Severity == ValidationSeverity.Warning));

        return Task.FromResult(new ValidationReport
        {
            FundId = result.FundId,
            ReportDate = result.ReportDate,
            Passed = passed,
            Findings = findings
        });
    }
}
