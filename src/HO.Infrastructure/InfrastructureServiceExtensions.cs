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
        // NOTE: No EnableRetryOnFailure here — it conflicts with EnsureDeleted/EnsureCreated
        // because the retry logic holds connections open across the drop→create cycle.
        // Re-add EnableRetryOnFailure in production once DB is stable.
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

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

        // ISignalRService via scoped factory — safe for HO.API context (no SignalR hub)
        services.AddScoped<ISignalRService>(sp =>
        {
            try
            {
                var hub = sp.GetService<IHubContext<DashboardHub>>();
                if (hub != null)
                {
                    var log = sp.GetRequiredService<ILogger<DashboardHubService>>();
                    return new DashboardHubService(hub, log);
                }
            }
            catch { /* SignalR not registered in this host */ }
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
