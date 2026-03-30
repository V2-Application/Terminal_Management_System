using Anthropic.SDK;
using HO.Application.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.AI;

/// <summary>
/// Extension methods for registering Claude AI services with the DI container.
/// </summary>
public static class AIServiceExtensions
{
    /// <summary>
    /// Register Claude AI service.
    /// Usage: services.AddClaudeAI(configuration);
    ///
    /// API key priority order:
    ///   1. Environment variable: ANTHROPIC_API_KEY
    ///   2. appsettings.json AISettings:ApiKey (dev only)
    /// </summary>
    public static IServiceCollection AddClaudeAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var aiSettings = configuration.GetSection("AISettings").Get<AISettings>() ?? new AISettings();
        services.AddSingleton(aiSettings);

        if (!aiSettings.Enabled)
        {
            // Register a no-op implementation when AI is disabled
            services.AddSingleton<IClaudeAIService, DisabledClaudeAIService>();
            return services;
        }

        // API key from env var (recommended) or config (dev only)
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                     ?? aiSettings.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Register disabled service with warning log
            services.AddSingleton<IClaudeAIService>(sp =>
            {
                sp.GetRequiredService<ILogger<ClaudeAIService>>()
                  .LogWarning("ANTHROPIC_API_KEY not configured — AI features disabled. " +
                              "Set environment variable ANTHROPIC_API_KEY to enable.");
                return new DisabledClaudeAIService();
            });
            return services;
        }

        // Register Anthropic client and service
        services.AddSingleton(new AnthropicClient(apiKey));
        services.AddScoped<IClaudeAIService, ClaudeAIService>();

        return services;
    }
}

/// <summary>
/// No-op implementation used when AI is disabled or API key is missing.
/// All methods return safe fallback responses without throwing.
/// </summary>
public class DisabledClaudeAIService : IClaudeAIService
{
    public Task<FailureDiagnosisResult> DiagnoseFailureAsync(
        string commandType, string storeCode, int exitCode,
        string stdout, string stderr, CancellationToken ct = default)
        => Task.FromResult(new FailureDiagnosisResult
        {
            RootCause = $"Exit code {exitCode}",
            RecommendedAction = "Check error log manually",
            ActionType = "MANUAL",
            IsSafeToRetry = false,
            Explanation = "AI analysis unavailable — ANTHROPIC_API_KEY not configured"
        });

    public Task<string> SummarizeBatchStatusAsync(
        int total, int completed, int failed, int offline, int pending,
        List<string> failedStoreNames, CancellationToken ct = default)
        => Task.FromResult(
            $"{completed}/{total} stores completed. Failed: {failed}. Offline: {offline}. Pending: {pending}.");

    public Task<string> AskAsync(string question, string context, CancellationToken ct = default)
        => Task.FromResult("AI assistant is currently disabled. Configure ANTHROPIC_API_KEY to enable.");

    public Task<RetryRecommendation> RecommendRetryActionAsync(
        string storeCode, string commandType, int exitCode,
        string errorOutput, int retryCount, CancellationToken ct = default)
        => Task.FromResult(new RetryRecommendation
        {
            Action = retryCount >= 3 ? "MANUAL_INTERVENTION" : "RETRY",
            Reason = "AI analysis unavailable — manual review required",
            SuggestedDelayMinutes = 5
        });
}
