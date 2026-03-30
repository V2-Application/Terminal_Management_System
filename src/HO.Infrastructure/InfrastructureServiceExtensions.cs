using HO.Application.AI;
using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Infrastructure.AI;
using HO.Infrastructure.Notifications;
using HO.Infrastructure.Persistence;
using HO.Infrastructure.Persistence.Repositories;
using HO.Infrastructure.Security;
using HO.Infrastructure.Services;
using HO.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(3)));

        // ── Repositories ─────────────────────────────────────────────────────
        services.AddScoped<IStoreRepository,    StoreRepository>();
        services.AddScoped<ITerminalRepository, TerminalRepository>();
        services.AddScoped<ICommandRepository,  CommandRepository>();
        services.AddScoped<IFYJobRepository,    FYJobRepository>();
        services.AddScoped<IPackageRepository,  PackageRepository>();

        // ── Application Services ─────────────────────────────────────────────
        services.AddScoped<ITerminalService, TerminalService>();
        services.AddScoped<ICommandService,  CommandService>();
        services.AddScoped<IFYCloseService,  FYCloseService>();

        // ── Cross-cutting ─────────────────────────────────────────────────────
        services.AddScoped<IAuditService,        AuditService>();
        services.AddScoped<INotificationService, EmailNotificationService>();

        // ISignalRService — uses factory so IHubContext<DashboardHub> is resolved
        // at request-time rather than startup, avoiding startup failure in HO.API
        // where SignalR is not mounted. Falls back to NullSignalRService if unavailable.
        services.AddScoped<ISignalRService>(sp =>
        {
            try
            {
                var hubContext = sp.GetService<IHubContext<DashboardHub>>();
                if (hubContext != null)
                {
                    var log = sp.GetRequiredService<ILogger<DashboardHubService>>();
                    return new DashboardHubService(hubContext, log);
                }
            }
            catch { /* SignalR not registered in this host (e.g., HO.API) */ }

            return new NullSignalRService();
        });

        // ── Security ─────────────────────────────────────────────────────────
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? new JwtSettings();
        services.AddSingleton(jwtSettings);
        services.AddSingleton<JwtService>();
        services.AddSingleton<PackageHashService>();

        // ── Claude AI ─────────────────────────────────────────────────────────
        services.AddHttpClient();
        services.AddClaudeAI(configuration);

        return services;
    }
}
