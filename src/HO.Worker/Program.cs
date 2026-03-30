using Hangfire;
using Hangfire.SqlServer;
using HO.Infrastructure.Jobs;
using HO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(opts => opts.ServiceName = "RetailTMS.Worker")
    .ConfigureServices((ctx, services) =>
    {
        var connectionString = ctx.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        // EF Core
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(connectionString));

        // Hangfire — SQL Server backing store
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName                  = "hangfire",
                CommandBatchMaxTimeout      = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout  = TimeSpan.FromMinutes(5),
                QueuePollInterval           = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks          = true
            }));

        services.AddHangfireServer(opts =>
        {
            opts.WorkerCount = 10;
            opts.Queues      = new[] { "fyclose", "monitoring", "alerts", "default" };
            opts.ServerName  = $"{Environment.MachineName}:worker";
        });

        // Hangfire job classes (DI)
        services.AddScoped<HeartbeatMonitorJob>();
        services.AddScoped<FYCloseOrchestratorJob>();
        services.AddScoped<WaveDispatchJob>();
        services.AddScoped<RetryFailedCommandsJob>();
    });

var host = builder.Build();

// Register recurring jobs on startup
using (var scope = host.Services.CreateScope())
{
    var mgr = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // Every 2 min — mark terminals offline if heartbeat missing
    mgr.AddOrUpdate<HeartbeatMonitorJob>(
        recurringJobId: "heartbeat-monitor",
        methodCall:     j => j.RunAsync(CancellationToken.None),
        cronExpression: "*/2 * * * *",
        options:        new RecurringJobOptions { QueueName = "monitoring" });

    // Every 15 min — auto-retry failed commands (up to MaxRetries)
    mgr.AddOrUpdate<RetryFailedCommandsJob>(
        recurringJobId: "retry-failed-commands",
        methodCall:     j => j.RunAsync(CancellationToken.None),
        cronExpression: "*/15 * * * *",
        options:        new RecurringJobOptions { QueueName = "default" });
}

await host.RunAsync();
