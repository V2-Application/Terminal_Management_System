using HO.Infrastructure;
using HO.Infrastructure.SignalR;   // DashboardHub

var builder = WebApplication.CreateBuilder(args);

// ⚠️  AddSignalR() MUST come before AddInfrastructure()
// so IHubContext<DashboardHub> is registered before DashboardHubService is wired up
builder.Services.AddSignalR();

// All repos, services, AI, SignalR service registered here
builder.Services.AddInfrastructure(builder.Configuration);

// MVC
builder.Services.AddControllersWithViews();

// MediatR for CQRS query handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(HO.Application.Queries.Dashboard.GetDashboardSummaryQuery).Assembly));

// Cookie auth for HO web users
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", opts =>
    {
        opts.LoginPath         = "/Account/Login";
        opts.LogoutPath        = "/Account/Logout";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan    = TimeSpan.FromHours(8);
        opts.Cookie.HttpOnly   = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opts.Cookie.SameSite   = SameSiteMode.Strict;
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
app.MapHub<DashboardHub>("/hubs/dashboard");   // Hub from HO.Infrastructure.SignalR

app.Run();
