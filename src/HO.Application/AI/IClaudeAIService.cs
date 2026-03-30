namespace HO.Application.AI;

/// <summary>
/// Claude AI service interface — abstracts Anthropic API calls.
/// Provides intelligent analysis for year-end close operations.
/// </summary>
public interface IClaudeAIService
{
    /// <summary>
    /// Analyze a failed command's stdout/stderr and suggest a fix.
    /// Returns structured diagnosis with root cause and recommended action.
    /// </summary>
    Task<FailureDiagnosisResult> DiagnoseFailureAsync(
        string commandType,
        string storeCode,
        int exitCode,
        string stdout,
        string stderr,
        CancellationToken ct = default);

    /// <summary>
    /// Summarize the overall FY-close batch status for the HO management report.
    /// Converts raw numbers into plain-English executive summary.
    /// </summary>
    Task<string> SummarizeBatchStatusAsync(
        int total, int completed, int failed, int offline, int pending,
        List<string> failedStoreNames,
        CancellationToken ct = default);

    /// <summary>
    /// Ask Claude a free-form question about the current batch state.
    /// Used by HO Operators from the dashboard chat panel.
    /// </summary>
    Task<string> AskAsync(string question, string context, CancellationToken ct = default);

    /// <summary>
    /// Recommend whether to retry, rollback, or escalate a failed store.
    /// Uses exit code + error pattern to make a data-driven recommendation.
    /// </summary>
    Task<RetryRecommendation> RecommendRetryActionAsync(
        string storeCode,
        string commandType,
        int exitCode,
        string errorOutput,
        int retryCount,
        CancellationToken ct = default);
}

public class FailureDiagnosisResult
{
    public string RootCause { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public string ActionType { get; set; } = "RETRY";  // RETRY, ROLLBACK, MANUAL, IGNORE
    public bool IsSafeToRetry { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class RetryRecommendation
{
    public string Action { get; set; } = "RETRY";  // RETRY, ROLLBACK, MANUAL_INTERVENTION
    public string Reason { get; set; } = string.Empty;
    public int SuggestedDelayMinutes { get; set; } = 5;
    public string[] ChecklistBeforeRetry { get; set; } = Array.Empty<string>();
}
