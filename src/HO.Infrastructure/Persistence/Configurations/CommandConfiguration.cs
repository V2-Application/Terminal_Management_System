using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class CommandConfiguration : IEntityTypeConfiguration<Command>
{
    public void Configure(EntityTypeBuilder<Command> builder)
    {
        builder.HasKey(c => c.CommandId);
        builder.HasIndex(c => new { c.TerminalId, c.CommandNonce }).IsUnique();
        builder.Property(c => c.CommandType).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => c.Status).HasFilter("[Status] IN ('Queued','Dispatched','Running')");
        builder.HasIndex(c => new { c.TerminalId, c.Status });
        builder.HasIndex(c => c.FYJobId);
        builder.HasOne(c => c.Terminal)
               .WithMany(t => t.Commands)
               .HasForeignKey(c => c.TerminalId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
