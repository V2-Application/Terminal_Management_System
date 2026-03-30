using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class HeartbeatConfiguration : IEntityTypeConfiguration<Heartbeat>
{
    public void Configure(EntityTypeBuilder<Heartbeat> b)
    {
        b.HasKey(h => h.HeartbeatId);
        b.HasIndex(h => new { h.TerminalId, h.ReceivedAt });
        b.Property(h => h.Status).HasMaxLength(20);
        b.Property(h => h.AgentVersion).HasMaxLength(20);
        b.HasOne(h => h.Terminal)
         .WithMany()
         .HasForeignKey(h => h.TerminalId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
