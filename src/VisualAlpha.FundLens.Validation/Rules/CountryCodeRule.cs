using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Validation.Rules;

public sealed class CountryCodeRule : IValidationRule
{
    public string RuleName => "CountryCode";
    public ValidationSeverity Severity => ValidationSeverity.Warning;

    // ISO 3166-1 alpha-3 (representative set — extend as needed)
    private static readonly HashSet<string> ValidIso3 = new(StringComparer.OrdinalIgnoreCase)
    {
        "USA", "GBR", "DEU", "FRA", "JPN", "CAN", "AUS", "CHE", "CHN", "IND",
        "KOR", "HKG", "SGP", "NLD", "SWE", "DNK", "NOR", "ESP", "ITA", "BRA",
        "ZAF", "MEX", "NZL", "PHL", "IDN", "THA", "MYS", "TWN", "ISR", "SAU",
        "ARE", "TUR", "POL", "CZE", "HUN", "PRT", "BEL", "AUT", "FIN", "IRL",
        "LUX", "RUS", "UKR", "EGY", "PER", "CHL", "COL", "ARG", "VNM", "ROU",
        "GRC", "ZZZ"
    };

    public IEnumerable<ValidationFinding> Validate(ExtractionResult result)
    {
        foreach (var (h, i) in result.Holdings.Select((h, i) => (h, i)))
        {
            if (h.Country is null) continue;  // Null is acceptable

            if (!ValidIso3.Contains(h.Country))
                yield return new ValidationFinding
                {
                    RuleName = RuleName,
                    Message = $"'{h.Country}' is not a valid ISO3 country code for holding '{h.SecurityName}'",
                    Severity = ValidationSeverity.Warning,
                    HoldingIndex = i,
                    FieldName = nameof(h.Country)
                };
        }
    }
}
