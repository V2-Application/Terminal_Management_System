using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class FinancialYearJobConfiguration : IEntityTypeConfiguration<FinancialYearJob>
{
    public void Configure(EntityTypeBuilder<FinancialYearJob> b)
    {
        b.HasKey(j => j.FYJobId);
        b.HasIndex(j => j.FYYear).IsUnique();
        b.Property(j => j.FYYear).HasMaxLength(10).IsRequired();
        b.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(j => j.Phase).HasConversion<string>().HasMaxLength(30);
        b.Property(j => j.StartedBy).HasMaxLength(100);
    }
}
