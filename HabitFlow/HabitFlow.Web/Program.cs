using HabitFlow.BLL.Interfaces;
using HabitFlow.BLL.Services;
using HabitFlow.DAL.Context;
using HabitFlow.DAL.Repositories;
using HabitFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Resend;


var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

Serilog.Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/habitflow.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IHabitRepository, HabitRepository>();
builder.Services.AddScoped<IHabitLogRepository, HabitLogRepository>();
builder.Services.AddScoped<ITriggerLogRepository, TriggerLogRepository>();
builder.Services.AddScoped<ISharedHabitRepository, SharedHabitRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IHabitService, HabitService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IBalanceConstellationService, BalanceConstellationService>();
builder.Services.AddScoped<ISharedHabitService, SharedHabitService>();
builder.Services.AddScoped<IQuitHabitService, QuitHabitService>();
builder.Services.AddScoped<IQuitAnalyticsService, QuitAnalyticsService>();

builder.Services.AddHttpClient<CoachService>();
builder.Services.AddScoped<ICoachService, CoachService>();
builder.Services.AddHttpClient<IQuitCoachService, QuitCoachService>();
builder.Services.AddHttpClient<IGoogleCalendarService, GoogleCalendarService>();

builder.Services.AddOptions();

builder.Services.AddHttpClient<ResendClient>();

builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiKey"]!;
});

builder.Services.AddTransient<IResend, ResendClient>();

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    if (path.StartsWithSegments("/Coach/VoiceStream"))
    {
        var log = context.RequestServices
            .GetRequiredService<ILogger<Program>>();
        log.LogInformation(
    "VoiceStream: IsHttps={IsHttps}, IsWebSocket={IsWs}",
    context.Request.IsHttps,
    context.WebSockets.IsWebSocketRequest);
    }

    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();