using HO.Application.AI;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HO.Infrastructure.AI;

/// <summary>
/// Anthropic Claude AI integration using raw HttpClient (no SDK dependency).
/// Calls the Anthropic Messages API directly — works with any .NET 8 project
/// without requiring any additional NuGet packages.
///
/// API Reference: https://docs.anthropic.com/en/api/messages
/// Model: claude-sonnet-4-6 (fast, cost-effective, excellent for log analysis)
/// </summary>
public class ClaudeAIService : IClaudeAIService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<ClaudeAIService> _logger;

    private const string ApiUrl    = "https://api.anthropic.com/v1/messages";
    private const string Model     = "claude-sonnet-4-6";
    private const string ApiVersion = "2023-06-01";

    // System prompt — RetailTMS context for Claude
    private const string SystemPrompt = """
        You are an expert IT operations assistant for a retail chain's centralized
        Terminal Management System (RetailTMS). Help IT staff at Head Office
        diagnose failures and make smart decisions during the 31st March
        financial year-end close process.

        The system manages 500+ store POS terminals (Microsoft Dynamics AX 2012 R3).
        Year-end close: deploy updated DLLs, clear IsolatedStorage config cache, sync clocks.

        Batch files: FY-CLOSE.BAT (kill POS → copy DLLs → clear cache),
        Time_set_before_Test_Bil.bat (sync to test NTP), Time_set_After_Billing.bat (sync to prod NTP).

        Common exit codes: 5=Access denied (AV blocking), 2=File not found, 1=General error.
        NET USE failures = network/credential issues. taskkill failures = POS hung.
        Always give concise, actionable advice.
        """;

    public ClaudeAIService(HttpClient http, string apiKey, ILogger<ClaudeAIService> logger)
    {
        _http   = http;
        _apiKey = apiKey;
        _logger = logger;
    }

    // ─── IClaudeAIService ────────────────────────────────────────────────────

    public async Task<FailureDiagnosisResult> DiagnoseFailureAsync(
        string commandType, string storeCode, int exitCode,
        string stdout, string stderr, CancellationToken ct = default)
    {
        _logger.LogInformation("Claude AI: Diagnosing {Type} failure at {Store} (exit={Exit})",
            commandType, storeCode, exitCode);

        var prompt = $"""
            A RetailTMS command FAILED. Diagnose it.

            Store: {storeCode}
            Command: {commandType}
            Exit Code: {exitCode}

            STDOUT (last 2000 chars):
            {Truncate(stdout, 2000)}

            STDERR (last 2000 chars):
            {Truncate(stderr, 2000)}

            Respond ONLY with valid JSON — no markdown, no explanation outside JSON:
            {{
              "rootCause": "one sentence describing what went wrong",
              "recommendedAction": "specific step to fix this",
              "actionType": "RETRY",
              "isSafeToRetry": true,
              "explanation": "detailed explanation for IT team"
            }}
            actionType must be one of: RETRY, ROLLBACK, MANUAL, IGNORE
            """;

        try
        {
            var raw = await CallAsync(prompt, maxTokens: 500, ct);
            var result = ParseJson<FailureDiagnosisResult>(raw);
            return result ?? FallbackDiagnosis(exitCode, stderr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI diagnosis failed — using fallback");
            return FallbackDiagnosis(exitCode, stderr);
        }
    }

    public async Task<string> SummarizeBatchStatusAsync(
        int total, int completed, int failed, int offline, int pending,
        List<string> failedStoreNames, CancellationToken ct = default)
    {
        var failList = failedStoreNames.Any()
            ? string.Join(", ", failedStoreNames.Take(10))
            : "none";

        var prompt = $"""
            Summarize this RetailTMS FY-Close batch for management. Write 2-3 sentences, plain English, no bullets.

            Total: {total} | Completed: {completed} ({(total > 0 ? completed * 100 / total : 0)}%)
            Failed: {failed} | Offline: {offline} | Pending: {pending}
            Failed stores: {failList}

            Mention if action needed and what.
            """;

        try { return await CallAsync(prompt, maxTokens: 150, ct); }
        catch { return $"{completed}/{total} completed. Failed: {failed}. Offline: {offline}."; }
    }

    public async Task<string> AskAsync(string question, string context, CancellationToken ct = default)
    {
        var prompt = $"""
            System context:
            {context}

            Operator question: {question}

            Answer in max 3 sentences, practically and concisely.
            """;

        try { return await CallAsync(prompt, maxTokens: 250, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI chat failed");
            return "AI assistant temporarily unavailable. Please check logs.";
        }
    }

    public async Task<RetryRecommendation> RecommendRetryActionAsync(
        string storeCode, string commandType, int exitCode,
        string errorOutput, int retryCount, CancellationToken ct = default)
    {
        var prompt = $"""
            RetailTMS command failed. Recommend next action.

            Store: {storeCode} | Command: {commandType} | Exit: {exitCode} | Retries: {retryCount}/3
            Error: {Truncate(errorOutput, 400)}

            Respond ONLY with JSON:
            {{
              "action": "RETRY",
              "reason": "why this action",
              "suggestedDelayMinutes": 5,
              "checklistBeforeRetry": ["check 1", "check 2"]
            }}
            action must be: RETRY, ROLLBACK, or MANUAL_INTERVENTION
            """;

        try
        {
            var raw = await CallAsync(prompt, maxTokens: 300, ct);
            return ParseJson<RetryRecommendation>(raw) ?? DefaultRetry(retryCount);
        }
        catch { return DefaultRetry(retryCount); }
    }

    // ─── Raw HTTP call to Anthropic API ─────────────────────────────────────

    private async Task<string> CallAsync(string userPrompt, int maxTokens, CancellationToken ct)
    {
        var body = new
        {
            model  = Model,
            max_tokens = maxTokens,
            system = SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json    = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = content
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Anthropic API error {Status}: {Body}", response.StatusCode, err);
            throw new HttpRequestException($"Anthropic API returned {response.StatusCode}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        // Response shape: { content: [ { type: "text", text: "..." } ] }
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        _logger.LogDebug("Claude response ({Tokens} tokens): {Preview}",
            maxTokens, text[..Math.Min(100, text.Length)]);

        return text;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static T? ParseJson<T>(string text) where T : class
    {
        try
        {
            // Extract JSON block if wrapped in markdown or other text
            var start = text.IndexOf('{');
            var end   = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            var jsonOnly = text[start..(end + 1)];
            return JsonSerializer.Deserialize<T>(jsonOnly,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        return s.Length <= max ? s : "..." + s[^max..];
    }

    private static FailureDiagnosisResult FallbackDiagnosis(int exitCode, string stderr) => new()
    {
        RootCause         = exitCode == 5 ? "Access denied — antivirus may be blocking DLL copy"
                          : exitCode == 2 ? "File not found — DLL package path incorrect on HO server"
                          : $"Script failed with exit code {exitCode}",
        RecommendedAction = exitCode == 5 ? "Temporarily disable AV at store, then retry"
                          : exitCode == 2 ? "Re-upload DLL package to HO server and retry"
                          : "Review full error log and contact IT support",
        ActionType   = exitCode is 5 or 2 ? "RETRY" : "MANUAL",
        IsSafeToRetry = exitCode is 5 or 2,
        Explanation  = $"[Fallback — Claude AI unavailable] Exit={exitCode}. Stderr: {Truncate(stderr, 200)}"
    };

    private static RetryRecommendation DefaultRetry(int retryCount) => new()
    {
        Action               = retryCount >= 3 ? "MANUAL_INTERVENTION" : "RETRY",
        Reason               = retryCount >= 3 ? "Max retries reached — manual fix needed" : "Retry may succeed",
        SuggestedDelayMinutes = retryCount * 5,
        ChecklistBeforeRetry = new[] { "Check store network", "Check HO file server" }
    };
}
