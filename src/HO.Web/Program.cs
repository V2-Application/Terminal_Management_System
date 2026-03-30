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

// Claude AI — reads ANTHROPIC_API_KEY from environment or appsettings
builder.Services.AddClaudeAI(builder.Configuration);

// Cookie Auth
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", opts =>
    {
        opts.LoginPath = "/Account/Login";
        opts.LogoutPath = "/Account/Logout";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan = TimeSpan.FromHours(8);
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
