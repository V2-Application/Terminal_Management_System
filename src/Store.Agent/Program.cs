using Microsoft.Extensions.DependencyInjection;  // AddHttpClient, AddScoped, etc.
using Microsoft.Extensions.Hosting;
using Serilog;
using Store.Agent;
using Store.Agent.Execution;
using Store.Agent.Models;
using Store.Agent.Security;
using Store.Agent.Services;

// Configure Serilog before host build
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: @"C:\RetailTMS\Logs\agent-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("RetailTMS Store Agent starting up");

    var host = Host.CreateDefaultBuilder(args)
        .UseWindowsService(opts => opts.ServiceName = "RetailTMS.StoreAgent")
        .UseSerilog()
        .ConfigureServices((ctx, services) =>
        {
            // Bind config
            var agentConfig = ctx.Configuration
                .GetSection("AgentConfig")
                .Get<AgentConfig>() ?? new AgentConfig();

            services.AddSingleton(agentConfig);

            // --- HTTP Client (Microsoft.Extensions.Http) ---
            services.AddHttpClient("HoApi", client =>
            {
                client.BaseAddress = new Uri(agentConfig.HoApiBaseUrl.TrimEnd('/') + '/');
                client.Timeout = TimeSpan.FromSeconds(agentConfig.DownloadTimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent",
                    $"RetailTMS-Agent/{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            });

            // --- Core Agent Services ---
            services.AddSingleton<CredentialStore>();
            services.AddSingleton<AgentConfig>(agentConfig);
            services.AddSingleton<LocalStateRepository>();
            services.AddSingleton<PreFlightChecker>();
            services.AddSingleton<PackageHashVerifier>();
            services.AddSingleton<ScriptExecutor>();

            services.AddScoped<HeartbeatService>();
            services.AddScoped<CommandPollerService>();
            services.AddScoped<ExecutionService>();
            services.AddScoped<BillingLockService>();

            // --- Background Worker ---
            services.AddHostedService<Worker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Store Agent terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
