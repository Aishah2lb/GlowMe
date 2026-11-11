using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

AppOptions.Bind(builder.Configuration);
GlowMeOptions.Bind(builder.Configuration);

var connection = builder.Configuration.GetConnectionString("Default") ?? string.Empty;
var dbPassword = builder.Configuration["Database:connections:sqlsrv:password"]
                 ?? builder.Configuration["DB_PASSWORD"];
if (!string.IsNullOrEmpty(dbPassword))
{
    connection = connection.Replace("Password=;", $"Password={dbPassword};");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connection, sql => sql.CommandTimeout(60));
});

AppServiceProvider.Register(builder.Services);

builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<MaintenanceService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<AccountLinks>();

builder.Services.AddScoped<IMailer, LogMailer>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RateLimiterStore>();

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
builder.Services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddHostedService<MaintenanceScheduler>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Session:cookie"] ?? "glow_me_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue("Session:lifetime", 15));
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });

builder.Services.AddAuthorization();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "glow_me_state";
    options.IdleTimeout = TimeSpan.FromMinutes(
        builder.Configuration.GetValue("Session:lifetime", 15));
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options => options.FormFieldName = "__RequestVerificationToken");

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()));

builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy(RouteServiceProvider.ApiLimiter, context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = RouteServiceProvider.ApiLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    limiter.AddPolicy("throttle:20,1", Fixed(20, 1));

    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static Func<HttpContext, RateLimitPartition<string>> Fixed(int max, int minutes) =>
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = max,
                Window = TimeSpan.FromMinutes(minutes),
                QueueLimit = 0,
            });
});

var app = builder.Build();

AppServiceProvider.Boot();
AuthServiceProvider.Boot();
BroadcastServiceProvider.Boot();

using (var scope = app.Services.CreateScope())
{
    DbSeeder.Run(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>());
}

app.UseMiddleware<ExceptionHandlerMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors();

app.UseMiddleware<PreventRequestsDuringMaintenanceMiddleware>();

app.UseMiddleware<TrimStringsMiddleware>();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();