using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Core.Domain;

/// <summary>Represents one validation finding</summary>
public sealed record ValidationFinding
{
    public required string RuleName { get; init; }
    public required string Message { get; init; }
    public ValidationSeverity Severity { get; init; }
    public int? HoldingIndex { get; init; } // null = fund-level finding
    public string? FieldName { get; init; }
}