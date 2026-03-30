using HO.Application.AI;
using HO.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.Web.Controllers;

/// <summary>
/// Controller for Claude AI assistant features in the HO dashboard.
/// All endpoints return JSON for AJAX consumption by the dashboard.
/// </summary>
[Authorize]
[Route("api/ai")]
public class AIAssistantController : Controller
{
    private readonly IClaudeAIService _ai;
    private readonly ICommandRepository _commandRepo;
    private readonly IFYJobRepository _fyJobRepo;
    private readonly IStoreRepository _storeRepo;
    private readonly ILogger<AIAssistantController> _logger;

    public AIAssistantController(
        IClaudeAIService ai,
        ICommandRepository commandRepo,
        IFYJobRepository fyJobRepo,
        IStoreRepository storeRepo,
        ILogger<AIAssistantController> logger)
    {
        _ai = ai;
        _commandRepo = commandRepo;
        _fyJobRepo = fyJobRepo;
        _storeRepo = storeRepo;
        _logger = logger;
    }

    /// <summary>
    /// Diagnose a specific failed command execution.
    /// Called when HO Operator clicks "AI Diagnose" on a failed store row.
    /// </summary>
    [HttpPost("diagnose/{commandId:guid}")]
    public async Task<JsonResult> Diagnose(Guid commandId, CancellationToken ct)
    {
        var command = await _commandRepo.GetByIdAsync(commandId, ct);
        if (command == null) return Json(new { error = "Command not found" });

        var execution = command.Executions.OrderByDescending(e => e.StartedAt).FirstOrDefault();
        var store = await _storeRepo.GetByIdAsync(command.StoreId, ct);

        var result = await _ai.DiagnoseFailureAsync(
            command.CommandType.ToString(),
            store?.StoreCode ?? "UNKNOWN",
            execution?.ExitCode ?? -1,
            execution?.Stdout ?? string.Empty,
            execution?.Stderr ?? string.Empty,
            ct);

        return Json(result);
    }

    /// <summary>
    /// Get AI retry recommendation for a failed store.
    /// </summary>
    [HttpPost("recommend/{commandId:guid}")]
    public async Task<JsonResult> Recommend(Guid commandId, CancellationToken ct)
    {
        var command = await _commandRepo.GetByIdAsync(commandId, ct);
        if (command == null) return Json(new { error = "Command not found" });

        var execution = command.Executions.OrderByDescending(e => e.StartedAt).FirstOrDefault();
        var store = await _storeRepo.GetByIdAsync(command.StoreId, ct);

        var result = await _ai.RecommendRetryActionAsync(
            store?.StoreCode ?? "UNKNOWN",
            command.CommandType.ToString(),
            execution?.ExitCode ?? -1,
            execution?.Stderr ?? string.Empty,
            command.RetryCount,
            ct);

        return Json(result);
    }

    /// <summary>
    /// Get an AI-generated plain-English summary of the current FY batch.
    /// Shown on the FY-Close dashboard as a management-friendly status update.
    /// </summary>
    [HttpGet("batch-summary")]
    public async Task<JsonResult> BatchSummary(CancellationToken ct)
    {
        var stores = (await _storeRepo.GetAllAsync(ct: ct)).ToList();
        var failedNames = stores
            .Where(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Failed)
            .Select(s => s.StoreName)
            .Take(10)
            .ToList();

        var summary = await _ai.SummarizeBatchStatusAsync(
            total:     stores.Count,
            completed: stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Completed),
            failed:    stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Failed),
            offline:   stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Offline),
            pending:   stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Pending),
            failedStoreNames: failedNames,
            ct);

        return Json(new { summary });
    }

    /// <summary>
    /// Free-form chat with Claude about the current FY-close operation.
    /// Used by the AI chat panel on the dashboard.
    /// </summary>
    [HttpPost("chat")]
    public async Task<JsonResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Json(new { answer = "Please enter a question." });

        // Build context from current system state
        var stores = (await _storeRepo.GetAllAsync(ct: ct)).ToList();
        var activeJob = await _fyJobRepo.GetActiveJobAsync(ct);

        var context = $"""
            Current FY-Close Batch:
            - FY Year: {activeJob?.FYYear ?? "Not started"}
            - Status: {activeJob?.Status}
            - Total Stores: {stores.Count}
            - Completed: {stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Completed)}
            - Failed: {stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Failed)}
            - Offline: {stores.Count(s => s.FYCloseStatus == HO.Domain.Enums.FYCloseStatus.Offline)}
            - Wave Size: {activeJob?.WaveSize ?? 0}
            """;

        var answer = await _ai.AskAsync(request.Message, context, ct);
        return Json(new { answer });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
