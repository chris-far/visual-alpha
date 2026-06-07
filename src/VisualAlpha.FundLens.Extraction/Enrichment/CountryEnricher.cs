using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Extraction.Enrichment;

/// <summary>
/// Maps security names and ISIN prefixes to ISO3 country codes.
/// Mappings are loaded from <see cref="CountryEnricherOptions"/> via configuration.
/// </summary>
public sealed class CountryEnricher : ICountryEnricher
{
    private static readonly Regex IsinPattern = new(@"\b([A-Z]{2})[A-Z0-9]{10}\b", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> NameToIso3 = new(StringComparer.OrdinalIgnoreCase) {{"China", "CHN"}, {"Other", "ZZZ"}};
    private static readonly Dictionary<string, string> Iso2ToIso3 = new(StringComparer.OrdinalIgnoreCase);

    private readonly CountryEnricherOptions _options;

    public CountryEnricher(IOptions<CountryEnricherOptions> options)
    {
        _options = options.Value;
        BuildCountryMappings();
    }

    public string? Enrich(string? country, string securityName)
    {
        // 1. Try country name
        if (country is not null && NameToIso3.TryGetValue(country, out var iso3))
            return iso3;
        
        // 2. Try country name as ISO2
        if (country is not null && Iso2ToIso3.TryGetValue(country, out var iso2ToIso3))
            return iso2ToIso3;
        
        // 2. Try ISIN embedded in security name
        var match = IsinPattern.Match(securityName);
        if (match.Success && _options.IsinToIso3.TryGetValue(match.Groups[1].Value, out var embedded))
            return embedded;
        
        return country;
    }

    private void BuildCountryMappings()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        foreach (var culture in cultures)
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var iso3 = region.ThreeLetterISORegionName;
                var iso2 = region.TwoLetterISORegionName;
                var englishName = region.EnglishName;
                var displayName = region.DisplayName;

                NameToIso3.TryAdd(englishName, iso3);
                NameToIso3.TryAdd(displayName, iso3);
                Iso2ToIso3.TryAdd(iso2, iso3);
            }
            catch (ArgumentException)
            {
                // rare or custom culture config
            }
        }
    }
}
