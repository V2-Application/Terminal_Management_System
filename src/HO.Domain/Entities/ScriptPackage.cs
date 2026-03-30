namespace HO.Domain.Entities;

public class ScriptPackage
{
    public Guid PackageId { get; set; } = Guid.NewGuid();
    public string PackageName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;  // NSO_SETUP, FY_CLOSE, etc.
    public string Version { get; set; } = string.Empty;
    public string? DllVersion { get; set; }
    public long FileSize { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? RsaSignature { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsRollbackPackage { get; set; }
    public string? FYYear { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
