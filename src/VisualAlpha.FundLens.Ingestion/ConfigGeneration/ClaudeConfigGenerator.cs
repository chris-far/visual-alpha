using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VisualAlpha.FundLens.Core.Domain;
using VisualAlpha.FundLens.Core.Interfaces;

namespace VisualAlpha.FundLens.Ingestion.ConfigGeneration;

public sealed class ClaudeConfigGenerator(
    AnthropicClient client,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeConfigGenerator> log) : IReportConfigGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ReportConfig> GenerateAsync(Stream pdfStream)
    {
        var sw = Stopwatch.StartNew();

        var pdfBytes = await ReadAllBytesAsync(pdfStream);
        var pdfBase64 = Convert.ToBase64String(pdfBytes);

        log.LogInformation("Calling Claude to generate report config ({Kb}KB PDF)", pdfBytes.Length / 1024);

        List<ContentBlockParam> content =
        [
            new DocumentBlockParam { Source = new Base64PdfSource { Data = pdfBase64 } },
            new TextBlockParam { Text = ReportConfigPromptBuilder.Build() }
        ];

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = options.Value.Model,
            MaxTokens = 8000,
            Messages = [new() { Role = Role.User, Content = content }]
        });

        var text = ExtractText(response);
        log.LogDebug("Claude response: {Text}", text);

        var config = Deserialise<ReportConfig>(text);

        log.LogInformation(
            "Report config generated: reportId={Id}, funds={Funds}, confidence={Score:P0}, issues={Issues}, elapsed={Elapsed}ms",
            config.ReportId, config.Funds.Count, config.ConfidenceScore, config.Issues.Count, sw.ElapsedMilliseconds);

        return config;
    }

    private static string ExtractText(Message response)
    {
        foreach (var block in response.Content)
            if (block.TryPickText(out var tb)) return tb.Text;
        throw new InvalidOperationException("No text content in Claude response");
    }

    private static T Deserialise<T>(string text)
    {
        var json = text.Replace("```json", "").Replace("```", "").Trim();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialise {typeof(T).Name}");
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
