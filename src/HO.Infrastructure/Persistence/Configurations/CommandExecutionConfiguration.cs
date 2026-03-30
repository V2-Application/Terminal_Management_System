using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HO.Infrastructure.Persistence.Configurations;

public class CommandExecutionConfiguration : IEntityTypeConfiguration<CommandExecution>
{
    public void Configure(EntityTypeBuilder<CommandExecution> b)
    {
        b.HasKey(e => e.ExecutionId);
        b.HasIndex(e => e.CommandId);
        b.HasIndex(e => new { e.TerminalId, e.StartedAt });
        b.Property(e => e.AgentVersion).HasMaxLength(20);
        b.HasOne(e => e.Command)
         .WithMany(c => c.Executions)
         .HasForeignKey(e => e.CommandId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
