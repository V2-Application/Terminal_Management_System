# Claude AI Integration Setup

RetailTMS uses [Anthropic's Claude AI](https://www.anthropic.com) to provide intelligent
failure diagnosis, batch status summaries, and retry recommendations.

## Features

| Feature | Description | Where |
|---------|-------------|-------|
| **Failure Diagnosis** | Analyzes stdout/stderr and explains root cause | Failed store row → "AI Diagnose" button |
| **Retry Recommendation** | RETRY / ROLLBACK / MANUAL decision with checklist | AI panel on dashboard |
| **Batch Summary** | Plain-English management summary of FY-close progress | Top of AI chat panel |
| **Free-form Chat** | Ask any question about current batch state | AI chat box on dashboard |

## Setup (5 minutes)

### Step 1 — Get API Key
1. Go to [console.anthropic.com](https://console.anthropic.com)
2. Sign up or log in
3. Navigate to **API Keys** → **Create Key**
4. Copy the key (starts with `sk-ant-...`)

### Step 2 — Configure the Key

#### Option A: Environment Variable (Recommended for production)
```powershell
# Windows — set system environment variable
[System.Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-YOUR_KEY", "Machine")

# Or set for current session only:
$env:ANTHROPIC_API_KEY = "sk-ant-YOUR_KEY"
```

#### Option B: appsettings.Development.json (Local dev only — never commit!)
```json
{
  "AISettings": {
    "Enabled": true,
    "ApiKey": "sk-ant-YOUR_KEY_HERE"
  }
}
```

**⚠️ NEVER put a real API key in `appsettings.json` (committed to git). Use environment variables.**

### Step 3 — Restart the Application
After setting the environment variable:
```powershell
iisreset
# or restart the app pool in IIS Manager
```

### Step 4 — Verify
Open the HO Dashboard. The AI panel should show:
- "Loading AI summary..." → then a real summary sentence
- The chat box should respond to questions

## Disabling AI
If you want to run without AI (e.g., no internet on HO server):
```json
{
  "AISettings": {
    "Enabled": false
  }
}
```
All AI features gracefully degrade — buttons still appear but return fallback responses.

## Cost Estimate
- Claude claude-sonnet-4-6 pricing: ~$3/million input tokens, ~$15/million output tokens
- A typical FY-close batch with 500 stores and ~50 failures: ≈ $0.10-$0.50 total
- Chat questions: ~$0.001 each

## Model Used
`claude-sonnet-4-6` (claude-sonnet-4-20250514) — fast, cost-effective, excellent for log analysis.
To change the model, edit `ClaudeAIService.cs`:
```csharp
Model = AnthropicModels.Claude35Sonnet,  // change this
```
