namespace HO.Application.AI;

/// <summary>
/// Configuration for Claude AI integration.
/// In production: store ApiKey in environment variable ANTHROPIC_API_KEY
/// or Azure Key Vault — never in appsettings.json directly.
/// </summary>
public class AISettings
{
    /// <summary>
    /// Anthropic API key. Set via environment variable ANTHROPIC_API_KEY
    /// or in appsettings.Development.json for local development only.
    /// NEVER commit a real API key to source control.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Whether AI features are enabled. Set false to disable without removing config.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Max tokens for AI responses (controls cost).</summary>
    public int MaxTokens { get; set; } = 500;
}
