namespace HO.Domain.Entities;

/// <summary>
/// Head Office user account — stored in dbo.HoUsers table.
/// Passwords are hashed with BCrypt (never stored plain text).
/// Roles: SuperAdmin | HOAdmin | HOOperator | Viewer
/// </summary>
public class HoUser
{
    public Guid   UserId       { get; set; } = Guid.NewGuid();
    public string Username     { get; set; } = string.Empty;
    public string FullName     { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;  // BCrypt hash
    public string Role         { get; set; } = "HOOperator";  // SuperAdmin|HOAdmin|HOOperator|Viewer
    public bool   IsActive     { get; set; } = true;
    public bool   MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public string?   LastLoginIp { get; set; }
    public int    FailedLoginCount { get; set; } = 0;
    public DateTime? LockedUntil   { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;
    public string  CreatedBy   { get; set; } = "SYSTEM";
}
