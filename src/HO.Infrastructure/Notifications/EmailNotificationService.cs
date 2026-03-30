using HO.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Notifications;

/// <summary>
/// Notification service — logs to console in dev, sends real email in production.
/// In production: replace the Send methods with SMTP (System.Net.Mail) or a provider (SendGrid, etc.)
/// </summary>
public class EmailNotificationService : INotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(ILogger<EmailNotificationService> logger)
        => _logger = logger;

    public async Task SendAlertAsync(string subject, string message, string severity = "INFO")
    {
        _logger.LogWarning("[ALERT/{Severity}] {Subject}: {Message}", severity, subject, message);
        // TODO: Replace with real SMTP or notification provider
        await Task.CompletedTask;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("[EMAIL] To={To} Subject={Subject}", to, subject);
        // TODO: Replace with real SMTP client
        await Task.CompletedTask;
    }
}
