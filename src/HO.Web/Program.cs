using HO.Infrastructure;
using HO.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Register all infrastructure (DB, repos, services, AI, SignalR service, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

// MVC
builder.Services.AddControllersWithViews();

// SignalR hub (browser dashboard real-time)
builder.Services.AddSignalR();

// MediatR for CQRS queries
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(HO.Application.Queries.Dashboard.GetDashboardSummaryQuery).Assembly));

// Cookie auth for HO web users
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", opts =>
    {
        opts.LoginPath        = "/Account/Login";
        opts.LogoutPath       = "/Account/Logout";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan   = TimeSpan.FromHours(8);
        opts.Cookie.HttpOnly  = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opts.Cookie.SameSite  = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();
