using Serilog;
using Store.Agent;
using Store.Agent.Execution;
using Store.Agent.Models;
using Store.Agent.Security;
using Store.Agent.Services;
using Store.Agent.Services;  // LocalStateRepository

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("C:\\RetailTMS\\Logs\\agent-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(opts => opts.ServiceName = "RetailTMS.StoreAgent")
    .UseSerilog()
    .ConfigureServices((ctx, services) =>
    {
        var agentConfig = ctx.Configuration.GetSection("AgentConfig").Get<AgentConfig>()
            ?? new AgentConfig();
        services.AddSingleton(agentConfig);

        // HTTP Client with token handler
        services.AddHttpClient("HoApi", client =>
        {
            client.BaseAddress = new Uri(agentConfig.HoApiBaseUrl.TrimEnd('/') + '/');
            client.Timeout = TimeSpan.FromSeconds(agentConfig.DownloadTimeoutSeconds);
        });

        // Agent services
        services.AddScoped<HeartbeatService>();
        services.AddScoped<CommandPollerService>();
        services.AddScoped<ExecutionService>();
        services.AddScoped<BillingLockService>();
        services.AddSingleton<CredentialStore>();
        services.AddSingleton<LocalStateRepository>();
        services.AddSingleton<PreFlightChecker>();
        services.AddSingleton<PackageHashVerifier>();
        services.AddSingleton<ScriptExecutor>();

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
