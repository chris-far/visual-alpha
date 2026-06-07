namespace VisualAlpha.FundLens.Ingestion.Storage;

public sealed class FileSystemConfigStoreOptions
{
    public const string SectionName = "FileSystemConfigStore";
    public required string ConfigStorePath { get; init; }
}
