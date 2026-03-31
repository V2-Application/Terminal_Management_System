using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HO.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<CommandExecution> CommandExecutions => Set<CommandExecution>();
    public DbSet<FinancialYearJob> FinancialYearJobs => Set<FinancialYearJob>();
    public DbSet<ScriptPackage> ScriptPackages => Set<ScriptPackage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Heartbeat> Heartbeats => Set<Heartbeat>();
    public DbSet<HoUser> HoUsers => Set<HoUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
