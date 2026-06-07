using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

public sealed record ValidationReport
{
    public required string FundId { get; init; }
    public DateOnly ReportDate { get; init; }
    public bool Passed { get; init; }
    public List<ValidationFinding> Findings { get; init; } = [];
    public int ErrorCount => Findings.Count(f => f.Severity >= ValidationSeverity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == ValidationSeverity.Warning);
}