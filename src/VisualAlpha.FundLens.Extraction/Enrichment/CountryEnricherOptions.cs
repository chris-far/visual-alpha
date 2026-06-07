namespace VisualAlpha.FundLens.Extraction.Enrichment;

public sealed class CountryEnricherOptions
{
    public const string SectionName = "CountryEnricher";

    // ISIN alpha-2 prefix → ISO3
    public Dictionary<string, string> IsinToIso3 { get; set; } = new();
}
