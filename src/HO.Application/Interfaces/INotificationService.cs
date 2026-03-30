namespace HO.Application.Interfaces;

public interface INotificationService
{
    Task SendAlertAsync(string subject, string message, string severity = "INFO");
    Task SendEmailAsync(string to, string subject, string body);
}
