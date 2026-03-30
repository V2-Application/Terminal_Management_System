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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HO.Infrastructure;

/// <summary>
/// Single entry point for registering ALL infrastructure services.
/// Call services.AddInfrastructure(config) from Program.cs in HO.API and HO.Web.
/// This is the only Infrastructure type that Program.cs needs to reference directly.
/// </summary>
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

        // ── Cross-cutting Services ────────────────────────────────────────────
        services.AddScoped<IAuditService,        AuditService>();
        services.AddScoped<INotificationService, EmailNotificationService>();
        services.AddScoped<ISignalRService,      DashboardHubService>();

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
