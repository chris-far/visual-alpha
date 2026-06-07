namespace VisualAlpha.FundLens.Core.Interfaces;

/// <summary>Enriches a country field using ISIN prefix or security name lookups</summary>
public interface ICountryEnricher
{
    string? Enrich(string? country, string securityName);
}