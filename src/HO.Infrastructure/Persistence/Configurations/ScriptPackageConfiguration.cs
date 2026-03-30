using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class ScriptPackageConfiguration : IEntityTypeConfiguration<ScriptPackage>
{
    public void Configure(EntityTypeBuilder<ScriptPackage> b)
    {
        b.HasKey(p => p.PackageId);
        b.Property(p => p.PackageName).HasMaxLength(100).IsRequired();
        b.Property(p => p.StepType).HasMaxLength(50).IsRequired();
        b.Property(p => p.Version).HasMaxLength(20).IsRequired();
        b.Property(p => p.Sha256Hash).HasMaxLength(64).IsRequired();
        b.Property(p => p.StoragePath).HasMaxLength(500).IsRequired();
        b.Property(p => p.UploadedBy).HasMaxLength(100).IsRequired();
        b.HasQueryFilter(p => !p.IsDeleted);
    }
}
