using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.HasKey(t => t.TerminalId);
        builder.HasIndex(t => t.TerminalCode).IsUnique();
        builder.HasIndex(t => t.MachineId).IsUnique();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.TerminalCode).HasMaxLength(30).IsRequired();
        builder.Property(t => t.MachineId).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.LastHeartbeat);
        builder.HasIndex(t => t.Status);
        builder.HasOne(t => t.Store)
               .WithMany(s => s.Terminals)
               .HasForeignKey(t => t.StoreId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
