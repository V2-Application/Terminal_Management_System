using HO.Application.AI;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HO.Infrastructure.AI;

/// <summary>
/// Anthropic Claude AI integration using raw HttpClient (no SDK dependency needed).
/// Calls https://api.anthropic.com/v1/messages directly with System.Net.Http + System.Text.Json.
/// Zero extra NuGet packages required beyond what .NET 8 already includes.
/// </summary>
public class ClaudeAIService : IClaudeAIService
{
    private readonly HttpClient _http;
    private readonly string     _apiKey;
    private readonly ILogger<ClaudeAIService> _logger;

    private const string ApiUrl     = "https://api.anthropic.com/v1/messages";
    private const string Model      = "claude-sonnet-4-6";
    private const string ApiVersion = "2023-06-01";

    private const string SystemPrompt =
        "You are an expert IT operations assistant for a retail chain's centralized " +
        "Terminal Management System (RetailTMS). Help HO IT staff diagnose failures " +
        "and make smart decisions during the 31st March financial year-end close process. " +
        "The system manages 500+ store POS terminals (Microsoft Dynamics AX 2012 R3). " +
        "Year-end close involves: deploying updated DLLs, clearing IsolatedStorage config cache, syncing clocks. " +
        "Batch files: FY-CLOSE.BAT (kill POS -> copy DLLs -> clear cache), " +
        "Time_set_before_Test_Bil.bat (sync to test NTP 192.168.144.131), " +
        "Time_set_After_Billing.bat (sync to prod NTP 192.168.144.158). " +
        "Common exit codes: 5=Access denied (AV blocking), 2=File not found, 1=General error. " +
        "NET USE failures = network or credential issues. taskkill failures = POS process hung. " +
        "Always give concise, actionable advice.";

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

        var prompt =
            "A RetailTMS command FAILED. Diagnose it.\n\n" +
            "Store: " + storeCode + "\n" +
            "Command: " + commandType + "\n" +
            "Exit Code: " + exitCode + "\n\n" +
            "STDOUT (last 2000 chars):\n" + Truncate(stdout, 2000) + "\n\n" +
            "STDERR (last 2000 chars):\n" + Truncate(stderr, 2000) + "\n\n" +
            "Respond ONLY with valid JSON (no markdown, no extra text):\n" +
            "{\n" +
            "  \"rootCause\": \"one sentence describing what went wrong\",\n" +
            "  \"recommendedAction\": \"specific step to fix this\",\n" +
            "  \"actionType\": \"RETRY\",\n" +
            "  \"isSafeToRetry\": true,\n" +
            "  \"explanation\": \"detailed explanation for the IT team\"\n" +
            "}\n" +
            "actionType must be one of: RETRY, ROLLBACK, MANUAL, IGNORE";

        try
        {
            var raw    = await CallAsync(prompt, maxTokens: 500, ct);
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
        var pct      = total > 0 ? completed * 100 / total : 0;
        var failList = failedStoreNames.Any()
            ? string.Join(", ", failedStoreNames.Take(10))
            : "none";

        var prompt =
            "Summarize this RetailTMS FY-Close batch for management. " +
            "Write 2-3 sentences, plain English, no bullet points.\n\n" +
            "Total: " + total + " | Completed: " + completed + " (" + pct + "%)\n" +
            "Failed: " + failed + " | Offline: " + offline + " | Pending: " + pending + "\n" +
            "Failed stores: " + failList + "\n\n" +
            "Mention if action is needed and what.";

        try   { return await CallAsync(prompt, maxTokens: 150, ct); }
        catch { return completed + "/" + total + " completed. Failed: " + failed + ". Offline: " + offline + "."; }
    }

    public async Task<string> AskAsync(
        string question, string context, CancellationToken ct = default)
    {
        var prompt =
            "System context:\n" + context + "\n\n" +
            "Operator question: " + question + "\n\n" +
            "Answer in max 3 sentences, practically and concisely.";

        try   { return await CallAsync(prompt, maxTokens: 250, ct); }
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
        var prompt =
            "RetailTMS command failed. Recommend the next action.\n\n" +
            "Store: " + storeCode + " | Command: " + commandType +
            " | Exit: " + exitCode + " | Retries: " + retryCount + "/3\n" +
            "Error: " + Truncate(errorOutput, 400) + "\n\n" +
            "Respond ONLY with JSON (no markdown):\n" +
            "{\n" +
            "  \"action\": \"RETRY\",\n" +
            "  \"reason\": \"why this action\",\n" +
            "  \"suggestedDelayMinutes\": 5,\n" +
            "  \"checklistBeforeRetry\": [\"check 1\", \"check 2\"]\n" +
            "}\n" +
            "action must be: RETRY, ROLLBACK, or MANUAL_INTERVENTION";

        try
        {
            var raw = await CallAsync(prompt, maxTokens: 300, ct);
            return ParseJson<RetryRecommendation>(raw) ?? DefaultRetry(retryCount);
        }
        catch { return DefaultRetry(retryCount); }
    }

    // ─── Raw HTTP call to Anthropic Messages API ─────────────────────────────

    private async Task<string> CallAsync(string userPrompt, int maxTokens, CancellationToken ct)
    {
        var bodyObj = new
        {
            model      = Model,
            max_tokens = maxTokens,
            system     = SystemPrompt,
            messages   = new[] { new { role = "user", content = userPrompt } }
        };

        var json    = JsonSerializer.Serialize(bodyObj);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = content };
        request.Headers.Add("x-api-key",         _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Anthropic API {Status}: {Body}", response.StatusCode, err);
            throw new HttpRequestException("Anthropic API returned " + response.StatusCode);
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc    = JsonDocument.Parse(responseJson);

        // Response: { "content": [ { "type": "text", "text": "..." } ] }
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        _logger.LogDebug("Claude response preview: {Preview}", text[..Math.Min(80, text.Length)]);
        return text;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static T? ParseJson<T>(string text) where T : class
    {
        try
        {
            var start = text.IndexOf('{');
            var end   = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return JsonSerializer.Deserialize<T>(
                text[start..(end + 1)],
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
                          : exitCode == 2 ? "File not found — DLL path incorrect on HO server"
                          : "Script failed with exit code " + exitCode,
        RecommendedAction = exitCode == 5 ? "Temporarily disable AV at store, then retry"
                          : exitCode == 2 ? "Re-upload DLL package and retry"
                          : "Review full error log and contact IT support",
        ActionType        = exitCode is 5 or 2 ? "RETRY" : "MANUAL",
        IsSafeToRetry     = exitCode is 5 or 2,
        Explanation       = "[Fallback — Claude AI unavailable] Exit=" + exitCode +
                            ". Stderr: " + Truncate(stderr, 200)
    };

    private static RetryRecommendation DefaultRetry(int retryCount) => new()
    {
        Action                = retryCount >= 3 ? "MANUAL_INTERVENTION" : "RETRY",
        Reason                = retryCount >= 3 ? "Max retries reached — manual fix needed" : "Retry may succeed",
        SuggestedDelayMinutes = retryCount * 5,
        ChecklistBeforeRetry  = new[] { "Check store network", "Check HO file server" }
    };
}
