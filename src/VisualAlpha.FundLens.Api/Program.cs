using System.Text.Json.Serialization;
using Anthropic;
using Microsoft.Extensions.Options;
using VisualAlpha.FundLens.Core.Interfaces;
using VisualAlpha.FundLens.Extraction.Core;
using VisualAlpha.FundLens.Extraction.Enrichment;
using VisualAlpha.FundLens.Extraction.Strategies;
using VisualAlpha.FundLens.Ingestion;
using VisualAlpha.FundLens.Ingestion.ConfigGeneration;
using VisualAlpha.FundLens.Ingestion.PreProcessing;
using VisualAlpha.FundLens.Ingestion.Storage;
using VisualAlpha.FundLens.Validation;
using VisualAlpha.FundLens.Validation.Rules;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetRequiredSection(AnthropicOptions.SectionName));
builder.Services.Configure<FileSystemConfigStoreOptions>(builder.Configuration.GetSection(FileSystemConfigStoreOptions.SectionName));

builder.Services.AddSingleton(sp => new AnthropicClient { ApiKey = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.ApiKey });
builder.Services.AddSingleton<IReportConfigGenerator, ClaudeConfigGenerator>();
builder.Services.AddSingleton<IColumnRangeResolver, ColumnRangeResolver>();

// Core Services
builder.Services.AddSingleton<IPdfPreProcessor, PdfPreProcessor>();
builder.Services.Configure<CountryEnricherOptions>(builder.Configuration.GetSection(CountryEnricherOptions.SectionName));
builder.Services.AddSingleton<ICountryEnricher, CountryEnricher>();
builder.Services.AddSingleton<IReportConfigStore, FileSystemConfigStore>();
builder.Services.AddSingleton<IReportOnboardingService, ReportOnboardingService>();

// Extraction Strategies
builder.Services.AddSingleton<IExtractionStrategy, ColumnBasedExtractionStrategy>();
builder.Services.AddSingleton<IHoldingExtractor, HoldingExtractor>();

// Validation Rules
builder.Services.AddSingleton<IValidationRule, SchemaValidationRule>();
builder.Services.AddSingleton<IValidationRule, MarketValueSumRule>();
builder.Services.AddSingleton<IValidationRule, ConfidenceThresholdRule>();
builder.Services.AddSingleton<IValidationRule, CountryCodeRule>();
builder.Services.AddSingleton<IValidationRunner, ValidationRunner>();

builder.Services.AddHttpLogging();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpLogging();
app.UseDefaultFiles(); // serves index.html as default
app.UseStaticFiles(); // serves wwwroot contents
app.MapControllers();
app.Run();