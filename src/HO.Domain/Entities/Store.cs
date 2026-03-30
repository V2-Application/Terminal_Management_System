using HO.Domain.Enums;

namespace HO.Domain.Entities;

public class Store
{
    public Guid StoreId { get; set; } = Guid.NewGuid();
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int Priority { get; set; } = 2;
    public StoreStatus Status { get; set; } = StoreStatus.Active;
    public FYCloseStatus FYCloseStatus { get; set; } = FYCloseStatus.Pending;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
}
