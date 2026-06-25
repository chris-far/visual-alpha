using System.Globalization;
using System.Text.RegularExpressions;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Enums;

namespace VisualAlpha.FundLens.Extraction.Core;

/// <summary>
/// Assembles raw text blocks into structured holdings.
/// Handles section headers, sub-headers, total rows, footnote markers, and cross-page state.
/// </summary>
public sealed class RowParser(ReportLayoutConfig layout)
{
    private string? _currentSecurityType;
    private string? _currentSector;
    private string? _currentCountry;

    private readonly Regex? _securityTypePattern = Compile(layout.SecurityTypePattern);
    private readonly Regex? _sectorPattern = Compile(layout.SectorPattern);
    private readonly Regex? _countryPattern = Compile(layout.CountryPattern);
    private readonly Regex? _totalRowPattern = Compile(layout.SubtotalRowPattern?.Regex);
    private readonly Regex? _footnoteMarker = Compile(layout.FootnotePattern?.Regex, RegexOptions.Compiled);
    private readonly Regex? _securityNameCleaning = Compile(layout.SecurityNameCleaningPattern?.Regex);
    
    public HoldingRecord? TryParseRow(
        Dictionary<FieldType, string> mapped,
        List<TextBlock> rawBlocks,
        int pageNumber,
        IReadOnlySet<FieldType> requiredFields)
    {
        var fullText = string.Join(" ", rawBlocks.Select(b => b.Text));

        if (_securityTypePattern?.IsMatch(fullText) == true)
        {
            _currentSecurityType = ExtractLabel(_securityTypePattern, fullText);
            return null;
        }

        if (_sectorPattern?.IsMatch(fullText) == true)
        {
            _currentSector = ExtractLabel(_sectorPattern, fullText);
            return null;
        }

        if (_countryPattern?.IsMatch(fullText) == true)
        {
            _currentCountry = ExtractLabel(_countryPattern, fullText);
            return null;
        }

        // Require every expected column to have a non-empty value
        if (!requiredFields.All(f => mapped.TryGetValue(f, out var v) && !string.IsNullOrWhiteSpace(v)))
            return null;

        var rawName = mapped[FieldType.SecurityName];
        if (_totalRowPattern?.IsMatch(rawName.Trim()) == true) return null;
        
        var (shares, principal) = MapPrincipalOrShares(mapped);
        if (mapped.TryGetValue(FieldType.Shares, out var sharesRaw))
            shares = ParseDecimal(sharesRaw);
        if (mapped.TryGetValue(FieldType.Principal, out var principalRaw))
            principal = ParseDecimal(principalRaw);

        var marketValue = ParseDecimal(mapped.GetValueOrDefault(FieldType.MarketValue));
        var securityName = CleanText(rawName);
        var warnings = new List<string>();
        var confidence = 1.0;

        if (marketValue is null) { warnings.Add("Market Value not extracted"); confidence -= 0.3; }
        if (securityName.Length < 3) { warnings.Add("Security Name suspiciously short"); confidence -= 0.2; }

        return new HoldingRecord
        {
            SecurityName = securityName,
            SecurityType = mapped.GetValueOrDefault(FieldType.SecurityType) ?? _currentSecurityType,
            Sector = mapped.GetValueOrDefault(FieldType.Sector) ?? _currentSector,
            Country = mapped.GetValueOrDefault(FieldType.Country) ?? _currentCountry,
            Shares = shares,
            Principal = principal,
            MarketValue = marketValue,
            SourcePageNumber = pageNumber,
            ExtractionConfidence = confidence,
            ExtractionWarnings = warnings
        };
    }

    private (decimal?, decimal?) MapPrincipalOrShares(Dictionary<FieldType, string> mapped)
    {
        decimal? shares = null, principal = null;
        if (!mapped.TryGetValue(FieldType.PrincipalOrShares, out var posRaw))
        {
            return (shares, principal);
        }
        
        var val = ParseDecimal(posRaw);
        if (val.HasValue)
        {
            // Treats as principal if the raw text contains a currency symbol, otherwise as shares.
            // TODO should be dynamic on asset type perhaps or think of a better way
            var hasCurrencySymbol = posRaw.Any(IsCurrencySymbol);
            shares = hasCurrencySymbol ? null : val;
            principal = hasCurrencySymbol ? val : null;
        } 
        
        return (shares, principal);
    }

    // If the pattern has a capture group, use group 1; otherwise strip common trailing noise.
    private static string ExtractLabel(Regex pattern, string text)
    {
        var m = pattern.Match(text);
        if (m.Groups.Count > 1 && m.Groups[1].Success)
        {
            return m.Groups[1].Value.Trim();
        }
        
        return Regex.Replace(m.Value, @"\s*[-–]\s*[\d.]+%.*$", "").Trim();
    }
    
    private static Regex? Compile(HeaderPattern? hp, RegexOptions opts = RegexOptions.IgnoreCase | RegexOptions.Compiled)
        => hp?.Regex is not null ? new Regex(hp.Regex, opts) : null;

    private static Regex? Compile(string? pattern, RegexOptions opts = RegexOptions.IgnoreCase | RegexOptions.Compiled)
        => pattern is not null ? new Regex(pattern, opts) : null;

    private static bool IsCurrencySymbol(char c)
        => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol;

    private decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = new string(raw.Where(c => !IsCurrencySymbol(c)).ToArray()).Replace(",", "").Trim();
        if (decimal.TryParse(cleaned, out var d))
        {
            return d;
        }
        
        var stripped = _footnoteMarker?.Replace(cleaned, "") ?? raw;
        return decimal.TryParse(stripped, out d) ? d : null;
    }
    
    private string CleanText(string raw)
    {
        return _securityNameCleaning?.Replace(raw.Trim(), "") ?? raw.Trim();
    }
}
