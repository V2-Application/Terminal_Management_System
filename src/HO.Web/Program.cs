using HO.Infrastructure;
using HO.Infrastructure.Persistence;
using HO.Infrastructure.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddInfrastructure(builder.Configuration);

// MVC with JSON options to handle circular refs + enum as strings
builder.Services.AddControllersWithViews()
    .AddJsonOptions(opts => {
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.WriteIndented = false;
    });

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
        opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opts.Cookie.SameSite   = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// Allow antiforgery token via custom header (for AJAX fetch calls)
builder.Services.AddAntiforgery(opts =>
{
    opts.HeaderName = "RequestVerificationToken";  // matches what JS sends
});

var app = builder.Build();

// DB init — create tables + seed if needed
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Logging.ILogger<AppDbContext>>();
    await DatabaseInitializer.InitializeAsync(db, logger);
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
