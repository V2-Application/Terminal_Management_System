using Hangfire;
using Hangfire.SqlServer;
using HO.Infrastructure.Jobs;
using HO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService(opts => opts.ServiceName = "RetailTMS.Worker")
    .ConfigureServices((ctx, services) =>
    {
        var connectionString = ctx.Configuration.GetConnectionString("DefaultConnection")!;

        services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(connectionString));

        // Hangfire with SQL Server storage
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer(opts =>
        {
            opts.WorkerCount = 10;
            opts.Queues = new[] { "fyclose", "monitoring", "alerts", "default" };
            opts.ServerName = Environment.MachineName + ":worker";
        });

        // Register job classes with DI
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

    // Every 2 minutes — detect offline terminals
    mgr.AddOrUpdate<HeartbeatMonitorJob>(
        "heartbeat-monitor",
        job => job.RunAsync(CancellationToken.None),
        "*/2 * * * *",
        new RecurringJobOptions { QueueName = "monitoring" });

    // Every 15 minutes — retry failed commands
    mgr.AddOrUpdate<RetryFailedCommandsJob>(
        "retry-failed-commands",
        job => job.RunAsync(CancellationToken.None),
        "*/15 * * * *",
        new RecurringJobOptions { QueueName = "default" });
}

await host.RunAsync();
