using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(a => a.AuditId);
        b.Property(a => a.AuditId).ValueGeneratedOnAdd();
        b.HasIndex(a => a.Timestamp);
        b.HasIndex(a => new { a.EntityType, a.EntityId });
        b.Property(a => a.UserId).HasMaxLength(100).IsRequired();
        b.Property(a => a.Action).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        b.Property(a => a.Result).HasMaxLength(20);
        b.Property(a => a.IpAddress).HasMaxLength(50);
    }
}
