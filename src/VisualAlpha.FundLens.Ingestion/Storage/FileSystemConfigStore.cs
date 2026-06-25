using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Ingestion.Storage;

/// <summary>
/// Stores ReportConfig files as JSON on the local file system.
/// For production, swap with a database-backed implementation.
/// </summary>
public sealed class FileSystemConfigStore(IOptions<FileSystemConfigStoreOptions> options, ILogger<FileSystemConfigStore> log) : IReportConfigStore
{
    private readonly string _basePath = options.Value.ConfigStorePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ReportConfig?> GetAsync(string reportId)
    {
        var path = ConfigPath(reportId);
        if (!File.Exists(path)) return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ReportConfig>(stream, JsonOptions);
    }

    public async Task SaveAsync(ReportConfig config)
    {
        Directory.CreateDirectory(_basePath);
        var path = ConfigPath(config.ReportId);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions);
        log.LogInformation("Config saved: {Path}", path);
    }

    public async Task<IReadOnlyList<ReportConfig>> GetAllAsync()
    {
        if (!Directory.Exists(_basePath)) return [];

        var configs = new List<ReportConfig>();
        foreach (var file in Directory.EnumerateFiles(_basePath, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var cfg = await JsonSerializer.DeserializeAsync<ReportConfig>(stream, JsonOptions);
            if (cfg is not null) configs.Add(cfg);
        }
        return configs;
    }

    public Task<bool> DeleteAsync(string reportId)
    {
        var path = ConfigPath(reportId);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        log.LogInformation("Config deleted: {Path}", path);
        return Task.FromResult(true);
    }

    private string ConfigPath(string reportId) => Path.Combine(_basePath, $"{reportId}.config.json");
}
