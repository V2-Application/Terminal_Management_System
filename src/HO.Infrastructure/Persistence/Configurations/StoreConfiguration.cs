using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.HasKey(s => s.StoreId);
        builder.Property(s => s.StoreId).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.HasIndex(s => s.StoreCode).IsUnique();
        builder.Property(s => s.StoreCode).HasMaxLength(20).IsRequired();
        builder.Property(s => s.StoreName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Region).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Zone).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.FYCloseStatus).HasConversion<string>().HasMaxLength(20);
        builder.HasQueryFilter(s => !s.IsDeleted);
        builder.HasIndex(s => s.Region);
        builder.HasIndex(s => s.FYCloseStatus);
    }
}
