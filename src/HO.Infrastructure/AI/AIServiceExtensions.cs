using HO.Application.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.AI;

/// <summary>
/// Registers Claude AI services — uses raw HttpClient, no NuGet SDK needed.
///
/// API key priority:
///   1. Environment variable:  ANTHROPIC_API_KEY  (recommended for prod)
///   2. appsettings.json:      AISettings:ApiKey  (dev only — never commit a real key)
/// </summary>
public static class AIServiceExtensions
{
    public static IServiceCollection AddClaudeAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("AISettings").Get<AISettings>() ?? new AISettings();
        services.AddSingleton(settings);

        if (!settings.Enabled)
        {
            services.AddSingleton<IClaudeAIService, DisabledClaudeAIService>();
            return services;
        }

        // Resolve API key: env var first, then config
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                     ?? settings.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<IClaudeAIService>(sp =>
            {
                sp.GetRequiredService<ILogger<DisabledClaudeAIService>>()
                  .LogWarning(
                    "Claude AI: ANTHROPIC_API_KEY not set. " +
                    "AI features disabled. Set env var ANTHROPIC_API_KEY to enable.");
                return new DisabledClaudeAIService();
            });
            return services;
        }

        // Register a named HttpClient for Anthropic API calls
        services.AddHttpClient("Anthropic", client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register the live Claude AI service
        services.AddScoped<IClaudeAIService>(sp =>
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var http        = httpFactory.CreateClient("Anthropic");
            var logger      = sp.GetRequiredService<ILogger<ClaudeAIService>>();
            return new ClaudeAIService(http, apiKey, logger);
        });

        return services;
    }
}

/// <summary>
/// No-op fallback when AI is disabled or API key is missing.
/// All methods return safe placeholder responses — never throws.
/// </summary>
public class DisabledClaudeAIService : IClaudeAIService
{
    private const string Msg = "AI assistant is disabled. Set ANTHROPIC_API_KEY to enable.";

    public Task<FailureDiagnosisResult> DiagnoseFailureAsync(
        string commandType, string storeCode, int exitCode,
        string stdout, string stderr, CancellationToken ct = default)
        => Task.FromResult(new FailureDiagnosisResult
        {
            RootCause         = $"Exit code {exitCode}",
            RecommendedAction = "Review error log manually",
            ActionType        = "MANUAL",
            IsSafeToRetry     = false,
            Explanation       = Msg
        });

    public Task<string> SummarizeBatchStatusAsync(
        int total, int completed, int failed, int offline, int pending,
        List<string> failedStoreNames, CancellationToken ct = default)
        => Task.FromResult($"{completed}/{total} stores completed. Failed: {failed}. Offline: {offline}.");

    public Task<string> AskAsync(string question, string context, CancellationToken ct = default)
        => Task.FromResult(Msg);

    public Task<RetryRecommendation> RecommendRetryActionAsync(
        string storeCode, string commandType, int exitCode,
        string errorOutput, int retryCount, CancellationToken ct = default)
        => Task.FromResult(new RetryRecommendation
        {
            Action                = retryCount >= 3 ? "MANUAL_INTERVENTION" : "RETRY",
            Reason                = Msg,
            SuggestedDelayMinutes = 5,
            ChecklistBeforeRetry  = Array.Empty<string>()
        });
}
