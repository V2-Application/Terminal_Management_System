using HO.Infrastructure.AI;
using HO.Infrastructure.Persistence;
using HO.Web.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MVC + SignalR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// HttpClient factory (needed by AI service and other HTTP calls)
builder.Services.AddHttpClient();

// Claude AI (reads ANTHROPIC_API_KEY from env var or appsettings)
builder.Services.AddClaudeAI(builder.Configuration);

// Cookie auth for HO users
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", opts =>
    {
        opts.LoginPath  = "/Account/Login";
        opts.LogoutPath = "/Account/Logout";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan = TimeSpan.FromHours(8);
        opts.Cookie.HttpOnly      = true;
        opts.Cookie.SecurePolicy  = CookieSecurePolicy.Always;
        opts.Cookie.SameSite      = SameSiteMode.Strict;
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
