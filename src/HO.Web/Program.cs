using HO.Infrastructure;
using HO.Infrastructure.Persistence;
using HO.Infrastructure.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ⚠️ AddSignalR MUST come before AddInfrastructure
builder.Services.AddSignalR();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(HO.Application.Queries.Dashboard.GetDashboardSummaryQuery).Assembly));

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", opts =>
    {
        opts.LoginPath         = "/Account/Login";
        opts.LogoutPath        = "/Account/Logout";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan    = TimeSpan.FromHours(8);
        opts.Cookie.HttpOnly   = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTP ok in dev
        opts.Cookie.SameSite   = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate DB and seed sample data on startup
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Logging.ILogger<AppDbContext>>();
    try
    {
        db.Database.Migrate();                      // apply any pending EF migrations
        await DatabaseSeeder.SeedAsync(db, logger); // seed demo data if empty
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "DB migration/seed failed — app will run without sample data. " +
            "Ensure SQL Server is running and connection string is correct.");
    }
}

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.Run();
