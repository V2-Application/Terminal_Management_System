using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using HO.Application.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HO.Infrastructure.AI;

/// <summary>
/// Anthropic Claude AI integration for RetailTMS.
/// Uses Claude claude-sonnet-4-6 for intelligent failure diagnosis, batch summarization,
/// and retry recommendations for year-end close operations.
///
/// Setup: Add ANTHROPIC_API_KEY to environment variables or appsettings.json.
/// </summary>
public class ClaudeAIService : IClaudeAIService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeAIService> _logger;

    // System prompt — gives Claude context about RetailTMS
    private const string SystemPrompt = """
        You are an expert IT operations assistant for a retail chain's centralized
        Terminal Management System (RetailTMS). Your role is to help IT staff at Head Office
        diagnose failures, understand system status, and make smart decisions during the
        31st March financial year-end close process.

        The system manages 500+ store POS terminals running Microsoft Dynamics AX 2012 R3 Retail POS.
        Year-end close involves: deploying updated DLLs, clearing config cache, syncing clocks.

        The batch files involved are:
        - IT_NSO_BATCH.BAT: New store setup (firewall, RDP, timezone, .NET 3.5, SAP config)
        - FY-CLOSE.BAT: Kill POS, copy new DLLs to Extensions folder, clear IsolatedStorage
        - Time_set_before_Test_Bil.bat: Sync clock to TEST server (192.168.144.131)
        - Time_set_After_Billing.bat: Sync clock to PROD server (192.168.144.158)

        Common failure causes:
        - Exit code 5: Access denied (antivirus blocking DLL copy)
        - Exit code 2: File not found (wrong path or missing DLLs on HO server)
        - Exit code 1: General script error
        - NET USE failures: Network connectivity or credential issues
        - taskkill failures: POS process hung or locked by another process

        Always give practical, actionable advice. Be concise. Use plain English.
        """;

    public ClaudeAIService(AnthropicClient client, ILogger<ClaudeAIService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FailureDiagnosisResult> DiagnoseFailureAsync(
        string commandType, string storeCode, int exitCode,
        string stdout, string stderr, CancellationToken ct = default)
    {
        _logger.LogInformation("Claude AI: Diagnosing failure for store {Store}, command {Type}, exit {Exit}",
            storeCode, commandType, exitCode);

        var prompt = $"""
            A RetailTMS command FAILED. Please diagnose the root cause and recommend an action.

            Store: {storeCode}
            Command Type: {commandType}
            Exit Code: {exitCode}

            STDOUT:
            {TruncateLog(stdout, 2000)}

            STDERR:
            {TruncateLog(stderr, 2000)}

            Respond in this exact JSON format (no markdown, just JSON):
            {{
              "rootCause": "Brief description of what went wrong",
              "recommendedAction": "Specific step to fix this",
              "actionType": "RETRY|ROLLBACK|MANUAL|IGNORE",
              "isSafeToRetry": true|false,
              "explanation": "Detailed explanation for the IT team"
            }}
            """;

        try
        {
            var response = await CallClaudeAsync(prompt, maxTokens: 500, ct);
            var json = ExtractJson(response);
            var result = JsonSerializer.Deserialize<FailureDiagnosisResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? FallbackDiagnosis(exitCode, stderr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude AI diagnosis failed — using fallback");
            return FallbackDiagnosis(exitCode, stderr);
        }
    }

    /// <inheritdoc/>
    public async Task<string> SummarizeBatchStatusAsync(
        int total, int completed, int failed, int offline, int pending,
        List<string> failedStoreNames, CancellationToken ct = default)
    {
        var failedList = failedStoreNames.Any()
            ? string.Join(", ", failedStoreNames.Take(10))
            : "none";

        var prompt = $"""
            Summarize this RetailTMS FY-Close batch status for the Head Office management team.
            Write 2-3 plain English sentences. Be direct and factual.

            Batch Stats:
            - Total stores: {total}
            - Completed: {completed} ({(total > 0 ? completed * 100 / total : 0)}%)
            - Failed: {failed}
            - Offline (pending reconnect): {offline}
            - Still pending: {pending}
            - Failed stores: {failedList}

            Mention if action is needed and what. Do not use bullet points.
            """;

        return await CallClaudeAsync(prompt, maxTokens: 200, ct);
    }

    /// <inheritdoc/>
    public async Task<string> AskAsync(string question, string context, CancellationToken ct = default)
    {
        var prompt = $"""
            Context about current system state:
            {context}

            Question from HO Operator: {question}

            Answer concisely and practically. Max 3 sentences.
            """;

        return await CallClaudeAsync(prompt, maxTokens: 300, ct);
    }

    /// <inheritdoc/>
    public async Task<RetryRecommendation> RecommendRetryActionAsync(
        string storeCode, string commandType, int exitCode,
        string errorOutput, int retryCount, CancellationToken ct = default)
    {
        var prompt = $"""
            A RetailTMS store command has failed. Recommend the best action.

            Store: {storeCode}
            Command: {commandType}
            Exit Code: {exitCode}
            Retry Count: {retryCount}/3
            Error: {TruncateLog(errorOutput, 500)}

            Respond in this exact JSON (no markdown):
            {{
              "action": "RETRY|ROLLBACK|MANUAL_INTERVENTION",
              "reason": "Why this action is recommended",
              "suggestedDelayMinutes": 5,
              "checklistBeforeRetry": ["item1", "item2"]
            }}
            """;

        try
        {
            var response = await CallClaudeAsync(prompt, maxTokens: 300, ct);
            var json = ExtractJson(response);
            return JsonSerializer.Deserialize<RetryRecommendation>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? DefaultRetryRecommendation(retryCount);
        }
        catch
        {
            return DefaultRetryRecommendation(retryCount);
        }
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private async Task<string> CallClaudeAsync(string userPrompt, int maxTokens, CancellationToken ct)
    {
        var request = new MessageParameters
        {
            Model = AnthropicModels.Claude35Sonnet,
            MaxTokens = maxTokens,
            System = new List<SystemMessage> { new SystemMessage(SystemPrompt) },
            Messages = new List<Message>
            {
                new Message(RoleType.User, userPrompt)
            }
        };

        var response = await _client.Messages.GetClaudeMessageAsync(request, ct);
        return response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }

    private static string TruncateLog(string log, int maxLen)
    {
        if (string.IsNullOrEmpty(log)) return "(empty)";
        return log.Length <= maxLen ? log : "..." + log[^maxLen..];
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static FailureDiagnosisResult FallbackDiagnosis(int exitCode, string stderr) => new()
    {
        RootCause = exitCode == 5 ? "Access denied — likely blocked by antivirus"
                  : exitCode == 2 ? "File not found — check DLL package path on HO server"
                  : $"Script failed with exit code {exitCode}",
        RecommendedAction = exitCode == 5 ? "Temporarily disable antivirus at store, then retry"
                          : exitCode == 2 ? "Verify package file exists on HO server, re-upload if needed"
                          : "Check full error log and contact IT support",
        ActionType = exitCode == 5 || exitCode == 2 ? "RETRY" : "MANUAL",
        IsSafeToRetry = exitCode is 5 or 2,
        Explanation = $"Automated diagnosis (Claude AI unavailable). Exit code: {exitCode}. Stderr: {TruncateLog(stderr, 200)}"
    };

    private static RetryRecommendation DefaultRetryRecommendation(int retryCount) => new()
    {
        Action = retryCount >= 3 ? "MANUAL_INTERVENTION" : "RETRY",
        Reason = retryCount >= 3
            ? "Maximum auto-retries reached. Manual investigation required."
            : "Transient failure — retry may succeed.",
        SuggestedDelayMinutes = retryCount * 5,
        ChecklistBeforeRetry = new[] { "Verify store network connectivity", "Check HO file server availability" }
    };
}
